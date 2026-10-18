package com.eirora.signin

import android.content.Context
import android.content.SharedPreferences
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.security.GeneralSecurityException
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/**
 * Small secrets, such as the refresh token, encrypted with AES-GCM under a key that never leaves the Android Keystore.
 * The ciphertext lives in shared preferences. Auto Backup may copy it to a new phone, but the key does not follow, so
 * there it reads as missing and the player signs in again.
 */
object SecureStore {
    private const val KEYSTORE = "AndroidKeyStore"
    private const val KEY_ALIAS = "eirora.secure-store"
    private const val PREFERENCES = "eirora.secure-store"
    private const val TRANSFORMATION = "AES/GCM/NoPadding"
    private const val IV_BYTES = 12
    private const val TAG_BITS = 128

    /** The value, or null if there is none or it cannot be decrypted here; an undecryptable value is removed. */
    @JvmStatic
    fun read(context: Context, key: String): String? {
        val stored = preferences(context).getString(key, null) ?: return null
        return try {
            val bytes = Base64.decode(stored, Base64.NO_WRAP)
            val cipher = Cipher.getInstance(TRANSFORMATION)
            cipher.init(Cipher.DECRYPT_MODE, secretKey(), GCMParameterSpec(TAG_BITS, bytes, 0, IV_BYTES))
            cipher.updateAAD(key.toByteArray(Charsets.UTF_8))
            String(cipher.doFinal(bytes, IV_BYTES, bytes.size - IV_BYTES), Charsets.UTF_8)
        } catch (e: GeneralSecurityException) {
            delete(context, key)
            null
        } catch (e: IllegalArgumentException) {
            delete(context, key)
            null
        }
    }

    /** Encrypts [value] with [key] as associated data, so a ciphertext cannot be moved to another key. */
    @JvmStatic
    fun write(context: Context, key: String, value: String) {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, secretKey())
        cipher.updateAAD(key.toByteArray(Charsets.UTF_8))
        val encrypted = cipher.iv + cipher.doFinal(value.toByteArray(Charsets.UTF_8))
        preferences(context).edit().putString(key, Base64.encodeToString(encrypted, Base64.NO_WRAP)).commit()
    }

    @JvmStatic
    fun delete(context: Context, key: String) {
        preferences(context).edit().remove(key).commit()
    }

    private fun preferences(context: Context): SharedPreferences =
        context.applicationContext.getSharedPreferences(PREFERENCES, Context.MODE_PRIVATE)

    @Synchronized
    private fun secretKey(): SecretKey {
        val keyStore = KeyStore.getInstance(KEYSTORE).apply { load(null) }
        (keyStore.getKey(KEY_ALIAS, null) as? SecretKey)?.let { return it }
        val generator = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE)
        generator.init(
            KeyGenParameterSpec.Builder(KEY_ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                .build(),
        )
        return generator.generateKey()
    }
}
