// The contribution key in iCloud Keychain (ADR-0008, TASK-023b), so that a new iPhone signed in to the same Apple
// Account reads the contribution part without asking. Called from Eirora.Client.Accounts.IosKeyBackup. iCloud Keychain
// is end-to-end encrypted and needs a passcode; without one the item stays on this phone. Nothing here logs the key.

#import <Foundation/Foundation.h>
#import <LocalAuthentication/LocalAuthentication.h>
#import <Security/Security.h>

static NSString* EiroraKeyBackupString(const char* text)
{
    return text == NULL ? nil : [NSString stringWithUTF8String:text];
}

// A synchronizable item in its own service, apart from the refresh token, which never leaves the device.
static NSMutableDictionary* EiroraKeyBackupQuery(const char* name)
{
    return [@{
        (__bridge id)kSecClass: (__bridge id)kSecClassGenericPassword,
        (__bridge id)kSecAttrService: @"com.eirora.contribution-key",
        (__bridge id)kSecAttrAccount: EiroraKeyBackupString(name),
        (__bridge id)kSecAttrSynchronizable: @YES,
    } mutableCopy];
}

extern "C"
{
    // The value, or NULL; the caller (IL2CPP's marshaller) frees the copy.
    char* EiroraKeyBackupRead(const char* name)
    {
        NSMutableDictionary* query = EiroraKeyBackupQuery(name);
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

    // 1 when the phone has a passcode, so the item reaches iCloud Keychain; 0 when it stays here; -1 when it was not kept.
    // Synchronizable items cannot be "this device only", so they are readable after the first unlock, like the refresh token.
    int EiroraKeyBackupKeep(const char* name, const char* value)
    {
        NSData* data = [EiroraKeyBackupString(value) dataUsingEncoding:NSUTF8StringEncoding];
        NSMutableDictionary* query = EiroraKeyBackupQuery(name);
        SecItemDelete((__bridge CFDictionaryRef)query);
        query[(__bridge id)kSecValueData] = data;
        query[(__bridge id)kSecAttrAccessible] = (__bridge id)kSecAttrAccessibleAfterFirstUnlock;
        if (SecItemAdd((__bridge CFDictionaryRef)query, NULL) != errSecSuccess)
        {
            return -1;
        }

        return [[LAContext new] canEvaluatePolicy:LAPolicyDeviceOwnerAuthentication error:nil] ? 1 : 0;
    }

    // Removes the item here and, through iCloud Keychain, on the player's other devices.
    void EiroraKeyBackupForget(const char* name)
    {
        SecItemDelete((__bridge CFDictionaryRef)EiroraKeyBackupQuery(name));
    }
}
