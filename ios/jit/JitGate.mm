#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#include <unistd.h>

extern "C" int csops(pid_t, unsigned int, void *, size_t);
static BOOL (*originalLaunch)(id, SEL, UIApplication *, NSDictionary *);
static NSMutableDictionary<NSString *, NSValue *> *lifecycleMethods;
static BOOL engineStarted;

@interface STS2JitGate : NSObject
@property(nonatomic, strong) id service;
@property(nonatomic, strong) UIApplication *application;
@property(nonatomic, copy) NSDictionary *launchOptions;
@property(nonatomic, strong) UIWindow *window;
@property(nonatomic, strong) UILabel *notice;
@property(nonatomic, strong) NSTimer *timer;
@property(nonatomic, copy) NSString *documents;
- (void)start;
- (void)checkReadiness;
@end

static STS2JitGate *startupGate;

static BOOL LaunchWhenReady(id service, SEL selector, UIApplication *application, NSDictionary *options) {
    startupGate = [STS2JitGate new];
    startupGate.service = service;
    startupGate.application = application;
    startupGate.launchOptions = options;
    [startupGate start];
    return YES;
}

static void ForwardLifecycle(id service, SEL selector, UIApplication *application) {
    // Godot's lifecycle callbacks dereference its OS singleton before checking it.
    if (!engineStarted) return;
    auto original = (void (*)(id, SEL, UIApplication *))lifecycleMethods[NSStringFromSelector(selector)].pointerValue;
    original(service, selector, application);
}

@implementation STS2JitGate
+ (void)load {
    // Main::setup loads the PCK during didFinishLaunching, before setupView runs.
    Class service = NSClassFromString(@"GDTAppDelegateService");
    Method launch = class_getInstanceMethod(service, @selector(application:didFinishLaunchingWithOptions:));
    if (!launch) abort();
    originalLaunch = (BOOL (*)(id, SEL, UIApplication *, NSDictionary *))method_setImplementation(launch, (IMP)LaunchWhenReady);
    lifecycleMethods = [NSMutableDictionary new];
    for (NSString *name in @[@"applicationDidBecomeActive:", @"applicationWillResignActive:",
             @"applicationDidEnterBackground:", @"applicationWillEnterForeground:",
             @"applicationDidReceiveMemoryWarning:", @"applicationWillTerminate:"]) {
        Method method = class_getInstanceMethod(service, NSSelectorFromString(name));
        if (!method) abort();
        lifecycleMethods[name] = [NSValue valueWithPointer:(const void *)method_setImplementation(method, (IMP)ForwardLifecycle)];
    }
}

- (void)start {
    self.documents = NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES).firstObject;
    UIViewController *controller = [UIViewController new];
    controller.view.backgroundColor = UIColor.systemBackgroundColor;
    self.notice = [UILabel new];
    self.notice.translatesAutoresizingMaskIntoConstraints = NO;
    self.notice.textColor = UIColor.labelColor;
    self.notice.font = [UIFont preferredFontForTextStyle:UIFontTextStyleTitle3];
    self.notice.textAlignment = NSTextAlignmentCenter;
    self.notice.numberOfLines = 0;
    [controller.view addSubview:self.notice];
    UILayoutGuide *area = controller.view.safeAreaLayoutGuide;
    [NSLayoutConstraint activateConstraints:@[
        [self.notice.centerYAnchor constraintEqualToAnchor:area.centerYAnchor],
        [self.notice.leadingAnchor constraintEqualToAnchor:area.leadingAnchor constant:24],
        [self.notice.trailingAnchor constraintEqualToAnchor:area.trailingAnchor constant:-24],
    ]];
    self.window = [[UIWindow alloc] initWithFrame:UIScreen.mainScreen.bounds];
    self.window.rootViewController = controller;
    [self.service setValue:self.window forKey:@"window"];
    [self.window makeKeyAndVisible];

    NSError *error = nil;
    NSFileManager *files = NSFileManager.defaultManager;
    NSString *mods = [self.documents stringByAppendingPathComponent:@"mods"];
    NSString *readme = [self.documents stringByAppendingPathComponent:@"SETUP.txt"];
    NSString *instructions = @"StS2 JIT\n\nCopy the prepared StS2.pck into this folder.\n"
        @"Copy mod folders into mods. Keep their original filenames.\n"
        @"To restore saves, copy default and mod_configs here as well.\n\n"
        @"Return to StS2 JIT and enable JIT for it using your JIT tool.\n"
        @"JIT must be enabled again whenever the app process restarts.\n";
    if (![files createDirectoryAtPath:mods withIntermediateDirectories:YES attributes:nil error:&error] ||
        (![files fileExistsAtPath:readme] && ![instructions writeToFile:readme atomically:YES encoding:NSUTF8StringEncoding error:&error])) {
        self.notice.text = [@"StS2 JIT\n\nCould not prepare the Files folder.\n" stringByAppendingString:error.localizedDescription];
        return;
    }

    __weak STS2JitGate *gate = self;
    self.timer = [NSTimer scheduledTimerWithTimeInterval:0.5 repeats:YES block:^(NSTimer *timer) {
        [gate checkReadiness];
    }];
    self.notice.text = @"StS2 JIT\n\nPreparing...";
}

- (void)checkReadiness {
    if (self.application.applicationState != UIApplicationStateActive) return;
    NSString *pack = [self.documents stringByAppendingPathComponent:@"StS2.pck"];
    NSDictionary *attributes = [NSFileManager.defaultManager attributesOfItemAtPath:pack error:nil];
    unsigned long long expectedSize = [NSBundle.mainBundle.infoDictionary[@"STS2ContentSize"] unsignedLongLongValue];
    BOOL complete = [[attributes fileType] isEqualToString:NSFileTypeRegular] && expectedSize > 0 && [attributes fileSize] == expectedSize;
    if (!complete) {
        self.notice.text = @"StS2 JIT\n\nGame content is missing or still copying.\n\n"
            @"Open Files > On My iPad > StS2 JIT.\nCopy the prepared StS2.pck into that folder.\n"
            @"Copy your mod folders into mods, then return here.\n\n"
            @"Keep the filename StS2.pck and wait for the copy to finish.";
        return;
    }
    uint32_t flags = 0;
    if (csops(getpid(), 0, &flags, sizeof(flags)) != 0 || !(flags & 0x10000000)) {
        self.notice.text = @"StS2 JIT\n\nGame content is ready.\n\n"
            @"Enable JIT for this app with your JIT tool or a Mac debugger.\n"
            @"The game will start automatically when JIT is ready.\n\n"
            @"JIT must be enabled again after the app process restarts.";
        return;
    }
    [self.timer invalidate];
    self.timer = nil;
    self.notice.text = @"StS2 JIT\n\nStarting game...";
    originalLaunch(self.service, @selector(application:didFinishLaunchingWithOptions:), self.application, self.launchOptions);
    engineStarted = YES;
    // The initial active notification was deliberately withheld while waiting.
    ForwardLifecycle(self.service, @selector(applicationDidBecomeActive:), self.application);
    self.window.hidden = YES;
    startupGate = nil;
}
@end
