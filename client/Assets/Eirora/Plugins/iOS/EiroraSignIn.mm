// Eirora's sign-in on iOS (ADR-0008): native Sign in with Apple, a web sign-in in ASWebAuthenticationSession for Google,
// and the keychain for the refresh token. Called from Eirora.Client.Accounts.IosSignIn; each request answers exactly once,
// on the main thread, through the callback C# registers. Nothing here logs, stores or sends a token.

#import <AuthenticationServices/AuthenticationServices.h>
#import <Foundation/Foundation.h>
#import <Security/Security.h>
#import <UIKit/UIKit.h>

extern UIViewController* UnityGetGLViewController();

// status: 0 signed in (first: ID token or callback URL, second: Apple's authorization code or NULL), 1 cancelled, 2 failed (first: reason).
typedef void (*EiroraSignInCallback)(int requestId, int status, const char* first, const char* second);

static EiroraSignInCallback s_callback = NULL;

static void EiroraAnswer(int requestId, int status, NSString* first, NSString* second)
{
    dispatch_async(dispatch_get_main_queue(), ^{
        if (s_callback != NULL)
        {
            s_callback(requestId, status, first.UTF8String, second.UTF8String);
        }
    });
}

@interface EiroraSignInDelegate : NSObject <ASAuthorizationControllerDelegate, ASAuthorizationControllerPresentationContextProviding, ASWebAuthenticationPresentationContextProviding>
@property (nonatomic) int requestId;
@property (nonatomic, strong) id retained;
@end

// Keeps each delegate, and the controller or session it serves, alive until it has answered.
static NSMutableSet<EiroraSignInDelegate*>* s_pending;

@implementation EiroraSignInDelegate

- (void)finish:(int)status first:(NSString*)first second:(NSString*)second
{
    EiroraAnswer(self.requestId, status, first, second);
    self.retained = nil;
    [s_pending removeObject:self];
}

- (void)authorizationController:(ASAuthorizationController*)controller didCompleteWithAuthorization:(ASAuthorization*)authorization
{
    ASAuthorizationAppleIDCredential* credential = (ASAuthorizationAppleIDCredential*)authorization.credential;
    if (![credential isKindOfClass:[ASAuthorizationAppleIDCredential class]] || credential.identityToken == nil)
    {
        [self finish:2 first:@"Apple sent no ID token" second:nil];
        return;
    }

    NSString* idToken = [[NSString alloc] initWithData:credential.identityToken encoding:NSUTF8StringEncoding];
    NSString* code = credential.authorizationCode == nil ? nil : [[NSString alloc] initWithData:credential.authorizationCode encoding:NSUTF8StringEncoding];
    [self finish:0 first:idToken second:code];
}

- (void)authorizationController:(ASAuthorizationController*)controller didCompleteWithError:(NSError*)error
{
    BOOL cancelled = [error.domain isEqualToString:ASAuthorizationErrorDomain] && error.code == ASAuthorizationErrorCanceled;
    [self finish:cancelled ? 1 : 2 first:[NSString stringWithFormat:@"Apple error %ld", (long)error.code] second:nil];
}

- (ASPresentationAnchor)presentationAnchorForAuthorizationController:(ASAuthorizationController*)controller
{
    return UnityGetGLViewController().view.window;
}

- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession*)session
{
    return UnityGetGLViewController().view.window;
}

@end

static EiroraSignInDelegate* EiroraNewDelegate(int requestId)
{
    if (s_pending == nil)
    {
        s_pending = [NSMutableSet set];
    }

    EiroraSignInDelegate* delegate = [EiroraSignInDelegate new];
    delegate.requestId = requestId;
    [s_pending addObject:delegate];
    return delegate;
}

static NSString* EiroraString(const char* text)
{
    return text == NULL ? nil : [NSString stringWithUTF8String:text];
}

static NSMutableDictionary* EiroraKeychainQuery(const char* key)
{
    return [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: @"com.eirora.account",
        (__bridge id)kSecAttrAccount: EiroraString(key),
    } mutableCopy];
}

extern "C"
{
    void EiroraSignInSetCallback(EiroraSignInCallback callback)
    {
        s_callback = callback;
    }

    // Sign in with Apple without any scope, so Apple sends neither name nor email (D-72).
    void EiroraSignInWithApple(int requestId, const char* nonceHash)
    {
        NSString* nonce = EiroraString(nonceHash);
        dispatch_async(dispatch_get_main_queue(), ^{
            ASAuthorizationAppleIDRequest* request = [[ASAuthorizationAppleIDProvider new] createRequest];
            request.requestedScopes = @[];
            request.nonce = nonce;
            ASAuthorizationController* controller = [[ASAuthorizationController alloc] initWithAuthorizationRequests:@[request]];
            EiroraSignInDelegate* delegate = EiroraNewDelegate(requestId);
            delegate.retained = controller;
            controller.delegate = delegate;
            controller.presentationContextProvider = delegate;
            [controller performRequests];
        });
    }

    // Opens url in ASWebAuthenticationSession and answers with the URL it ends on with callbackScheme.
    void EiroraWebAuthenticate(int requestId, const char* url, const char* callbackScheme)
    {
        NSURL* address = [NSURL URLWithString:EiroraString(url)];
        NSString* scheme = EiroraString(callbackScheme);
        dispatch_async(dispatch_get_main_queue(), ^{
            EiroraSignInDelegate* delegate = EiroraNewDelegate(requestId);
            ASWebAuthenticationSession* session = [[ASWebAuthenticationSession alloc]
                initWithURL:address
                callbackURLScheme:scheme
                completionHandler:^(NSURL* callbackURL, NSError* error) {
                    if (callbackURL != nil)
                    {
                        [delegate finish:0 first:callbackURL.absoluteString second:nil];
                        return;
                    }

                    BOOL cancelled = [error.domain isEqualToString:ASWebAuthenticationSessionErrorDomain]
                        && error.code == ASWebAuthenticationSessionErrorCodeCanceledLogin;
                    [delegate finish:cancelled ? 1 : 2 first:[NSString stringWithFormat:@"Web sign-in error %ld", (long)error.code] second:nil];
                }];
            session.presentationContextProvider = delegate;
            delegate.retained = session;
            if (![session start])
            {
                [delegate finish:2 first:@"The web sign-in could not start" second:nil];
            }
        });
    }

    // The value, or NULL; the caller (IL2CPP's marshaller) frees the copy.
    char* EiroraKeychainRead(const char* key)
    {
        NSMutableDictionary* query = EiroraKeychainQuery(key);
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef result = NULL;
        if (SecItemCopyMatching((__bridge CFDictionaryRef)query, &result) != errSecSuccess || result == NULL)
        {
            return NULL;
        }

        NSData* data = (__bridge_transfer NSData*)result;
        NSString* value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        return value == nil ? NULL : strdup(value.UTF8String);
    }

    // Only on this device and after its first unlock: never synced to iCloud or restored to another phone.
    bool EiroraKeychainWrite(const char* key, const char* value)
    {
        NSData* data = [EiroraString(value) dataUsingEncoding:NSUTF8StringEncoding];
        NSMutableDictionary* query = EiroraKeychainQuery(key);
        SecItemDelete((__bridge CFDictionaryRef)query);
        query[(__bridge id)kSecValueData] = data;
        query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly;
        return SecItemAdd((__bridge CFDictionaryRef)query, NULL) == errSecSuccess;
    }

    void EiroraKeychainDelete(const char* key)
    {
        SecItemDelete((__bridge CFDictionaryRef)EiroraKeychainQuery(key));
    }
}
