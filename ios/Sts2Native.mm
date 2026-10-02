#import <AVFoundation/AVFoundation.h>
#import <Security/Security.h>
#import <UIKit/UIKit.h>
#import <CoreImage/CoreImage.h>
#include <arpa/inet.h>

#define STS2_EXPORT extern "C" __attribute__((visibility("default"), used))

STS2_EXPORT int sts2_audio_activate() {
    AVAudioSession *session = [AVAudioSession sharedInstance];
    NSError *error = nil;
    if (![session setCategory:AVAudioSessionCategoryPlayback mode:AVAudioSessionModeDefault options:0 error:&error])
        return (int)error.code;
    if (![session setActive:YES error:&error])
        return (int)error.code;
    return 0;
}

static NSDictionary *credentialQuery() {
    return @{(__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: [NSBundle.mainBundle.bundleIdentifier stringByAppendingString:@".steam"],
        (__bridge id)kSecAttrAccount: @"refresh-token"};
}

STS2_EXPORT int sts2_keychain_write(const char *value) {
    NSData *data = [[NSString stringWithUTF8String:value] dataUsingEncoding:NSUTF8StringEncoding];
    NSDictionary *attributes = @{(__bridge id)kSecValueData: data,
        (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleWhenUnlockedThisDeviceOnly};
    OSStatus status = SecItemUpdate((__bridge CFDictionaryRef)credentialQuery(), (__bridge CFDictionaryRef)attributes);
    if (status == errSecItemNotFound) {
        NSMutableDictionary *query = [credentialQuery() mutableCopy];
        [query addEntriesFromDictionary:attributes];
        status = SecItemAdd((__bridge CFDictionaryRef)query, nullptr);
    }
    return (int)status;
}

STS2_EXPORT char *sts2_keychain_read() {
    NSMutableDictionary *query = [credentialQuery() mutableCopy];
    query[(__bridge id)kSecReturnData] = @YES;
    CFTypeRef result = nullptr;
    if (SecItemCopyMatching((__bridge CFDictionaryRef)query, &result) != errSecSuccess)
        return nullptr;
    NSData *data = (__bridge_transfer NSData *)result;
    NSString *value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
    return value ? strdup(value.UTF8String) : nullptr;
}

STS2_EXPORT int sts2_keychain_delete() {
    OSStatus status = SecItemDelete((__bridge CFDictionaryRef)credentialQuery());
    return status == errSecItemNotFound ? 0 : (int)status;
}

STS2_EXPORT void sts2_free(void *pointer) { free(pointer); }

STS2_EXPORT char *sts2_qr_png(const char *value) {
    NSData *data = [[NSString stringWithUTF8String:value] dataUsingEncoding:NSUTF8StringEncoding];
    CIFilter *filter = [CIFilter filterWithName:@"CIQRCodeGenerator"];
    [filter setValue:data forKey:@"inputMessage"];
    [filter setValue:@"M" forKey:@"inputCorrectionLevel"];
    CIImage *image = [filter.outputImage imageByApplyingTransform:CGAffineTransformMakeScale(8, 8)];
    CIContext *context = [CIContext contextWithOptions:nil];
    CGImageRef cgImage = [context createCGImage:image fromRect:image.extent];
    if (!cgImage) return nullptr;
    NSData *png = UIImagePNGRepresentation([UIImage imageWithCGImage:cgImage]);
    CGImageRelease(cgImage);
    return strdup([png base64EncodedStringWithOptions:0].UTF8String);
}

// Bonjour lives on the main run loop; Godot reads a bounded JSON snapshot.
@interface Sts2LanDiscovery : NSObject <NSNetServiceBrowserDelegate, NSNetServiceDelegate>
@property(nonatomic, strong) NSNetServiceBrowser *browser;
@property(nonatomic, strong) NSNetService *published;
@property(nonatomic, strong) NSMutableArray<NSNetService *> *services;
@property(nonatomic, copy) NSString *browseError;
@end

@implementation Sts2LanDiscovery
- (instancetype)init {
    self = [super init];
    if (self) { self.services = [NSMutableArray array]; self.browseError = @""; }
    return self;
}
- (void)stopBrowsing {
    self.browser.delegate = nil;
    [self.browser stop]; self.browser = nil;
    for (NSNetService *service in self.services) { service.delegate = nil; [service stop]; }
    [self.services removeAllObjects];
}
- (void)netServiceBrowser:(NSNetServiceBrowser *)browser didFindService:(NSNetService *)service moreComing:(BOOL)more {
    if (browser != self.browser || self.services.count >= 64 || [self.services containsObject:service]) return;
    [self.services addObject:service];
    service.delegate = self;
    [service resolveWithTimeout:5];
}
- (void)netServiceBrowser:(NSNetServiceBrowser *)browser didRemoveService:(NSNetService *)service moreComing:(BOOL)more {
    if (browser != self.browser) return;
    NSUInteger index = [self.services indexOfObject:service];
    if (index != NSNotFound) {
        NSNetService *found = self.services[index]; found.delegate = nil; [found stop];
        [self.services removeObjectAtIndex:index];
    }
}
- (void)netServiceBrowser:(NSNetServiceBrowser *)browser didNotSearch:(NSDictionary *)error {
    if (browser == self.browser)
        self.browseError = @"Nearby search failed. Check Settings > Privacy & Security > Local Network, then try again.";
}
- (void)netService:(NSNetService *)service didNotResolve:(NSDictionary *)error {
    [service stop];
    [self.services removeObject:service];
}
- (NSString *)snapshot {
    NSMutableArray *hosts = [NSMutableArray array];
    for (NSNetService *service in self.services) {
        if (service.port <= 0 || service.port > 65535) continue;
        for (NSData *data in service.addresses) {
            if (data.length < sizeof(struct sockaddr_in)) continue;
            const struct sockaddr_in *address = (const struct sockaddr_in *)data.bytes;
            if (address->sin_family != AF_INET) continue;
            char ip[INET_ADDRSTRLEN];
            if (!inet_ntop(AF_INET, &address->sin_addr, ip, sizeof(ip))) continue;
            NSString *name = service.name ?: @"LAN host";
            if (name.length > 64) name = [name substringToIndex:64];
            [hosts addObject:@{@"name": name, @"address": @(ip), @"port": @(service.port)}];
            break;
        }
    }
    NSData *json = [NSJSONSerialization dataWithJSONObject:@{@"hosts": hosts, @"error": self.browseError} options:0 error:nil];
    return [[NSString alloc] initWithData:json encoding:NSUTF8StringEncoding];
}
@end

static Sts2LanDiscovery *sts2Lan;
static void lanOnMain(void (^action)(void)) {
    void (^run)(void) = ^{
        if (!sts2Lan) sts2Lan = [[Sts2LanDiscovery alloc] init];
        action();
    };
    if (NSThread.isMainThread) run();
    else dispatch_sync(dispatch_get_main_queue(), run);
}

STS2_EXPORT int sts2_lan_browse() {
    lanOnMain(^{
        [sts2Lan stopBrowsing]; sts2Lan.browseError = @"";
        sts2Lan.browser = [[NSNetServiceBrowser alloc] init];
        sts2Lan.browser.delegate = sts2Lan;
        [sts2Lan.browser searchForServicesOfType:@"_sts2lan._udp." inDomain:@"local."];
    });
    return 0;
}
STS2_EXPORT int sts2_lan_browse_stop() {
    lanOnMain(^{ [sts2Lan stopBrowsing]; }); return 0;
}
STS2_EXPORT int sts2_lan_publish(int port) {
    if (port <= 0 || port > 65535) return -1;
    lanOnMain(^{
        [sts2Lan.published stop];
        // Empty name lets Bonjour choose and disambiguate the device's local name.
        sts2Lan.published = [[NSNetService alloc] initWithDomain:@"local." type:@"_sts2lan._udp." name:@"" port:port];
        [sts2Lan.published publish];
    });
    return 0;
}
STS2_EXPORT int sts2_lan_publish_stop() {
    lanOnMain(^{ [sts2Lan.published stop]; sts2Lan.published = nil; }); return 0;
}
STS2_EXPORT char *sts2_lan_snapshot() {
    __block char *result = nullptr;
    lanOnMain(^{ result = strdup([sts2Lan snapshot].UTF8String); });
    return result;
}
