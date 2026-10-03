#include <sys/mman.h>
#include <mach/mach.h>
#include <pthread.h>
#include <map>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>
#include "MonoJitMemory.h"

struct Region { size_t size; uintptr_t writable; };
static std::map<uintptr_t, Region> regions;
static pthread_mutex_t mutex = PTHREAD_MUTEX_INITIALIZER;

extern "C" void sts2_jit_register(void *address, size_t size) {
    // Populate the shared backing pages before creating separate RX and RW views.
    // Leaving them zero-fill-on-demand makes the executable alias fault on iOS.
    memset(address, 0, size);
    if (mprotect(address, size, PROT_READ | PROT_EXEC) != 0) abort();
    vm_address_t alias = 0;
    vm_prot_t current = 0, maximum = 0;
    if (vm_remap(mach_task_self(), &alias, size, 0, VM_FLAGS_ANYWHERE,
            mach_task_self(), (vm_address_t)address, false, &current, &maximum, VM_INHERIT_NONE) != KERN_SUCCESS ||
            mprotect((void *)alias, size, PROT_READ | PROT_WRITE) != 0) {
        fprintf(stderr, "[JIT] Could not create writable code alias\n");
        abort();
    }
    pthread_mutex_lock(&mutex);
    regions.emplace((uintptr_t)address, Region{size, alias});
    pthread_mutex_unlock(&mutex);
}

extern "C" void sts2_jit_unregister(void *address) {
    pthread_mutex_lock(&mutex);
    auto it = regions.find((uintptr_t)address);
    if (it != regions.end()) {
        munmap((void *)it->second.writable, it->second.size);
        regions.erase(it);
    }
    pthread_mutex_unlock(&mutex);
}

extern "C" void *sts2_jit_writable(void *address, size_t size) {
    uintptr_t value = (uintptr_t)address;
    pthread_mutex_lock(&mutex);
    auto it = regions.upper_bound(value);
    if (it != regions.begin()) {
        --it;
        if (value >= it->first && value - it->first < it->second.size) {
            if (size > it->second.size - (value - it->first)) abort();
            address = (void *)(it->second.writable + value - it->first);
        }
    }
    pthread_mutex_unlock(&mutex);
    return address;
}
