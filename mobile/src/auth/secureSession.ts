import type { StoredTokens, TokenStore } from '@okul/api-client';
import * as SecureStore from 'expo-secure-store';

/**
 * The mobile session in the device's secure store — Keychain / Keystore (spec 0003, Requirement 8). Access and refresh
 * tokens never touch AsyncStorage or memory beyond the request that uses them.
 */
const tokensKey = 'okul.session';

/** Kurum kodu + e-posta, remembered for the next login (User experience) — never the password. */
const loginHintKey = 'okul.login-hint';

export interface LoginHint {
  institutionCode: string;
  email: string;
}

export interface SecureStorage {
  getItemAsync(key: string): Promise<string | null>;
  setItemAsync(key: string, value: string): Promise<void>;
  deleteItemAsync(key: string): Promise<void>;
}

export function secureTokenStore(storage: SecureStorage = SecureStore): TokenStore {
  return {
    async read() {
      const raw = await storage.getItemAsync(tokensKey);
      return raw ? (JSON.parse(raw) as StoredTokens) : null;
    },
    async write(tokens) {
      await storage.setItemAsync(tokensKey, JSON.stringify(tokens));
    },
    async clear() {
      await storage.deleteItemAsync(tokensKey);
    },
  };
}

export async function readLoginHint(storage: SecureStorage = SecureStore): Promise<LoginHint | null> {
  const raw = await storage.getItemAsync(loginHintKey);
  return raw ? (JSON.parse(raw) as LoginHint) : null;
}

export async function writeLoginHint(hint: LoginHint, storage: SecureStorage = SecureStore): Promise<void> {
  await storage.setItemAsync(loginHintKey, JSON.stringify(hint));
}
