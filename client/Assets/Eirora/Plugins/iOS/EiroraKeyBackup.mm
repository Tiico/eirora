// The contribution key in iCloud Keychain (ADR-0008, TASK-023b), so that a new iPhone signed in to the same Apple
// Account reads the contribution part without asking. Called from Eirora.Client.Accounts.IosKeyBackup. iCloud Keychain
// is end-to-end encrypted and needs a passcode; without one the item stays on this phone. Each call returns the
// keychain's OSStatus, so that C# tells "not there" from a failure; nothing here logs the key.

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

// 1 when the phone has a passcode, so the item reaches iCloud Keychain; 0 when it has none and the item stays here;
// -1 when the phone cannot tell, so that the account sheet claims nothing.
static int EiroraKeyBackupReach(void)
{
    NSError* error = nil;
    if ([[LAContext new] canEvaluatePolicy:LAPolicyDeviceOwnerAuthentication error:&error])
    {
        return 1;
    }

    return error.code == LAErrorPasscodeNotSet ? 0 : -1;
}

extern "C"
{
    // The value, or NULL with *status errSecItemNotFound when there is none or another OSStatus when the keychain
    // failed; the caller (IL2CPP's marshaller) frees the copy.
    char* EiroraKeyBackupRead(const char* name, int* status)
    {
        NSMutableDictionary* query = EiroraKeyBackupQuery(name);
        query[(__bridge id)kSecReturnData] = @YES;
        query[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef result = NULL;
        *status = SecItemCopyMatching((__bridge CFDictionaryRef)query, &result);
        if (*status != errSecSuccess || result == NULL)
        {
            return NULL;
        }

        NSData* data = (__bridge_transfer NSData*)result;
        NSString* value = [[NSString alloc] initWithData:data encoding:NSUTF8StringEncoding];
        if (value == nil)
        {
            *status = errSecDecode;
            return NULL;
        }

        return strdup(value.UTF8String);
    }

    // Keeps the value and returns errSecSuccess with *reach set (see EiroraKeyBackupReach), or the failing OSStatus. An
    // unchanged value is not written again, so that a launch sends nothing through iCloud Keychain; a changed one is
    // updated in place, never deleted first, so that the player's other devices never see it missing.
    // Synchronizable items cannot be "this device only", so they are readable after the first unlock, like the refresh token.
    int EiroraKeyBackupKeep(const char* name, const char* value, int* reach)
    {
        NSData* data = [EiroraKeyBackupString(value) dataUsingEncoding:NSUTF8StringEncoding];
        NSMutableDictionary* query = EiroraKeyBackupQuery(name);
        NSMutableDictionary* read = [query mutableCopy];
        read[(__bridge id)kSecReturnData] = @YES;
        read[(__bridge id)kSecMatchLimit] = (__bridge id)kSecMatchLimitOne;
        CFTypeRef result = NULL;
        OSStatus status = SecItemCopyMatching((__bridge CFDictionaryRef)read, &result);
        NSData* kept = status == errSecSuccess ? (__bridge_transfer NSData*)result : nil;
        NSDictionary* update = @{
            (__bridge id)kSecValueData: data,
            (__bridge id)kSecAttrAccessible: (__bridge id)kSecAttrAccessibleAfterFirstUnlock,
        };
        if (status == errSecSuccess && ![kept isEqualToData:data])
        {
            status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)update);
        }
        else if (status == errSecItemNotFound)
        {
            NSMutableDictionary* add = [query mutableCopy];
            [add addEntriesFromDictionary:update];
            status = SecItemAdd((__bridge CFDictionaryRef)add, NULL);
            if (status == errSecDuplicateItem)
            {
                status = SecItemUpdate((__bridge CFDictionaryRef)query, (__bridge CFDictionaryRef)update);
            }
        }

        if (status != errSecSuccess)
        {
            return status;
        }

        *reach = EiroraKeyBackupReach();
        return errSecSuccess;
    }

    // Removes the item here and, through iCloud Keychain, on the player's other devices; returns the OSStatus, which is
    // errSecItemNotFound when there was nothing to remove.
    int EiroraKeyBackupForget(const char* name)
    {
        return SecItemDelete((__bridge CFDictionaryRef)EiroraKeyBackupQuery(name));
    }
}
