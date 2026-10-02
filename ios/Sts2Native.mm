#import <AVFoundation/AVFoundation.h>
#import <Security/Security.h>
#import <UIKit/UIKit.h>
#import <CoreImage/CoreImage.h>

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
