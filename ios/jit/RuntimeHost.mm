#import <Foundation/Foundation.h>
#include <dlfcn.h>
#include <sys/mman.h>
#include <libkern/OSCacheControl.h>
#include <mono/jit/jit.h>
#include <mono/utils/mono-logger.h>
#include <mach/mach.h>
#include <errno.h>
#include <unistd.h>
#include "MonoJitMemory.h"

extern "C" int coreclr_initialize(const char *, const char *, int, const char **, const char **, void **, unsigned int *);
extern "C" int coreclr_create_delegate(void *, unsigned int, const char *, const char *, const char *, void **);
extern "C" int csops(pid_t, unsigned int, void *, size_t);

static bool initialized;
static void *runtime;
static unsigned int domain;

extern "C" __attribute__((visibility("default"))) void sts2_jit_log(const char *message) {
    fprintf(stderr, "%s\n", message);
}

extern "C" __attribute__((visibility("default"))) int sts2_jit_patch(
        void *target, const void *source, int size, void *backup, int backupSize) {
    if (!target || !source || size <= 0) return EINVAL;
    void *alias = sts2_jit_writable(target, size);
    if (alias != target) {
        if (backup && backupSize >= size) memcpy(backup, target, size);
        memcpy(alias, source, size);
        sys_icache_invalidate(target, size);
        return 0;
    }
    // Native/AOT executable detours need a separate backend; never revoke execute
    // permission from pages that another thread may currently be running.
    return ENOTSUP;
}

static bool CheckJitPermission() {
    uint32_t flags = 0;
    // CS_DEBUGGED survives debugger detachment for this process lifetime.
    if (csops(getpid(), 0, &flags, sizeof(flags)) != 0 || !(flags & 0x10000000)) return false;
    size_t size = getpagesize();
    void *memory = mmap(nullptr, size, PROT_READ | PROT_WRITE, MAP_PRIVATE | MAP_ANON, -1, 0);
    if (memory == MAP_FAILED) return false;
    // mov w0, #42; ret. Verify permission before allowing Mono to initialize.
    const uint32_t code[] = {0x52800540, 0xd65f03c0};
    memcpy(memory, code, sizeof(code));
    int result = mprotect(memory, size, PROT_READ | PROT_EXEC);
    vm_address_t address = (vm_address_t)memory;
    vm_size_t regionSize = 0;
    vm_region_basic_info_data_64_t info = {};
    mach_msg_type_number_t count = VM_REGION_BASIC_INFO_COUNT_64;
    mach_port_t object = MACH_PORT_NULL;
    kern_return_t queried = vm_region_64(mach_task_self(), &address, &regionSize, VM_REGION_BASIC_INFO_64, (vm_region_info_t)&info, &count, &object);
    if (object != MACH_PORT_NULL) mach_port_deallocate(mach_task_self(), object);
    bool ok = result == 0 && queried == KERN_SUCCESS && (info.protection & VM_PROT_EXECUTE) != 0;
    if (ok) {
        sys_icache_invalidate(memory, sizeof(code));
        ok = ((int (*)())memory)() == 42;
    }
    vm_address_t alias = 0;
    vm_prot_t current = 0, maximum = 0;
    kern_return_t mapped = vm_remap(mach_task_self(), &alias, size, 0, VM_FLAGS_ANYWHERE,
        mach_task_self(), (vm_address_t)memory, false, &current, &maximum, VM_INHERIT_NONE);
    if (mapped == KERN_SUCCESS) {
        int writable = mprotect((void *)alias, size, PROT_READ | PROT_WRITE);
        if (ok && writable == 0) {
            *(uint32_t *)alias = 0x52800560; // mov w0, #43
            sys_icache_invalidate(memory, sizeof(code));
            ok = ((int (*)())memory)() == 43;
        } else ok = false;
        munmap((void *)alias, size);
    } else ok = false;
    munmap(memory, size);
    return ok;
}

static bool InitializeRuntime() {
    if (initialized) return true;
    if (!CheckJitPermission()) {
        fprintf(stderr, "[JIT] Executable memory unavailable. Enable JIT for this process before starting.\n");
        return false;
    }
    fprintf(stderr, "[JIT] Executable memory available\n");
    mono_trace_set_log_handler([](const char *domain, const char *level, const char *message, mono_bool fatal, void *) {
        fprintf(stderr, "[Mono/%s] %s: %s\n", level ?: "", domain ?: "", message ?: "");
        if (fatal) abort();
    }, nullptr);
    NSString *managed = [NSBundle.mainBundle.resourcePath stringByAppendingPathComponent:@"Managed"];
    NSString *frameworks = NSBundle.mainBundle.privateFrameworksPath;
    NSMutableArray<NSString *> *assemblies = [NSMutableArray array];
    for (NSString *file in [NSFileManager.defaultManager contentsOfDirectoryAtPath:managed error:nil]) {
        if ([file.pathExtension isEqualToString:@"dll"])
            [assemblies addObject:[managed stringByAppendingPathComponent:file]];
    }
    NSString *tpa = [assemblies componentsJoinedByString:@":"];
    setenv("MONO_THREADS_SUSPEND", "preemptive", 1);
    const char *keys[] = {"TRUSTED_PLATFORM_ASSEMBLIES", "APP_PATHS", "APP_CONTEXT_BASE_DIRECTORY",
        "NATIVE_DLL_SEARCH_DIRECTORIES", "System.Globalization.Invariant", "System.Diagnostics.Tracing.EventSource.IsSupported"};
    const char *values[] = {tpa.UTF8String, managed.UTF8String, managed.UTF8String,
        frameworks.UTF8String, "false", "false"};
    int result = coreclr_initialize(nullptr, "StS2Jit", 6, keys, values, &runtime, &domain);
    if (result != 0) {
        fprintf(stderr, "[JIT] Runtime initialization failed: %x\n", result);
        return false;
    }
    initialized = true;
    return true;
}

extern "C" __attribute__((visibility("default"))) bool godotsharp_game_main_init(
        void *godot, void *callbacks, void *interop, int size) {
    NSString *logPath = [NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES).firstObject stringByAppendingPathComponent:@"jit-game.log"];
    freopen(logPath.fileSystemRepresentation, "w", stdout);
    dup2(fileno(stdout), fileno(stderr));
    setbuf(stdout, nullptr);
    setbuf(stderr, nullptr);
    if (!InitializeRuntime()) return false;
    bool (*entry)(void *, void *, void *, int) = nullptr;
    int result = coreclr_create_delegate(runtime, domain, "sts2", "GodotPlugins.Game.Main", "InitializeFromGameProject", (void **)&entry);
    if (result != 0 || !entry || !entry(godot ?: dlopen(nullptr, RTLD_NOW), callbacks, interop, size)) return false;
    int (*start)() = nullptr;
    result = coreclr_create_delegate(runtime, domain, "STS2Jit", "STS2Jit.Entry", "Start", (void **)&start);
    return result == 0 && start && start() == 0;
}
