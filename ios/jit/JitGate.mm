#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#include <unistd.h>

extern "C" int csops(pid_t, unsigned int, void *, size_t);
static BOOL (*originalSetup)(id, SEL, UIView *);
static UILabel *jitNotice;

static BOOL SetupWithJit(id renderer, SEL selector, UIView *view) {
    uint32_t flags = 0;
    if (csops(getpid(), 0, &flags, sizeof(flags)) != 0 || !(flags & 0x10000000)) {
        if (!jitNotice) {
            jitNotice = [[UILabel alloc] initWithFrame:view.bounds];
            jitNotice.autoresizingMask = UIViewAutoresizingFlexibleWidth | UIViewAutoresizingFlexibleHeight;
            jitNotice.backgroundColor = UIColor.systemBackgroundColor;
            jitNotice.textColor = UIColor.labelColor;
            jitNotice.textAlignment = NSTextAlignmentCenter;
            jitNotice.numberOfLines = 0;
            jitNotice.text = @"StS2 JIT\n\nEnable JIT for this app with your JIT tool or a Mac debugger.\nThe game will start automatically when JIT is ready.\n\nJIT must be enabled again after the app process restarts.";
            [view addSubview:jitNotice];
        }
        return YES;
    }
    [jitNotice removeFromSuperview];
    jitNotice = nil;
    return originalSetup(renderer, selector, view);
}

// Gate Godot before it initializes Mono, leaving UIKit responsive to external JIT tools.
@interface STS2JitGate : NSObject
@end
@implementation STS2JitGate
+ (void)load {
    Method setup = class_getInstanceMethod(NSClassFromString(@"GDTViewRenderer"), NSSelectorFromString(@"setupView:"));
    if (!setup) abort();
    originalSetup = (BOOL (*)(id, SEL, UIView *))method_setImplementation(setup, (IMP)SetupWithJit);
}
@end
