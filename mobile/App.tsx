import { createOkulClient, type OkulClient } from '@okul/api-client';
import { StatusBar } from 'expo-status-bar';
import { useCallback, useEffect, useMemo, useState } from 'react';
import { ActivityIndicator, SafeAreaView, StyleSheet } from 'react-native';
import { readLoginHint, secureTokenStore, writeLoginHint, type LoginHint } from './src/auth/secureSession';
import { InstitutionScreen } from './src/features/institution/InstitutionScreen';
import { LoginScreen, loginTexts } from './src/features/login/LoginScreen';
import { ChangePasswordScreen, passwordTexts } from './src/features/password/ChangePasswordScreen';

const baseUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';

/**
 * Mobile app (spec 0003). Tokens live in the device's secure store (Requirement 8); there is no automatic test login
 * any more (Requirement 9) — without a session the app opens on Giriş, with kurum kodu and e-posta remembered.
 */
export function createMobileClient(): OkulClient {
  return createOkulClient({ baseUrl, kind: 'mobile', tokenStore: secureTokenStore() });
}

type Screen =
  | { kind: 'checking' }
  | { kind: 'login'; notice?: string; hint: LoginHint | null }
  | { kind: 'institution'; notice?: string }
  | { kind: 'password' };

export interface AppProps {
  client?: OkulClient;
  loginHint?: { read: () => Promise<LoginHint | null>; write: (hint: LoginHint) => Promise<void> };
}

export default function App({ client: injected, loginHint }: AppProps = {}) {
  const client = useMemo(() => injected ?? createMobileClient(), [injected]);
  const hints = useMemo(() => loginHint ?? { read: () => readLoginHint(), write: (h: LoginHint) => writeLoginHint(h) }, [loginHint]);
  const [screen, setScreen] = useState<Screen>({ kind: 'checking' });

  const toLogin = useCallback(
    async (notice?: string) => setScreen({ kind: 'login', notice, hint: await hints.read() }),
    [hints],
  );

  useEffect(() => {
    let active = true;
    void client.hasSession().then(async (has) => {
      if (!active) return;
      if (has) setScreen({ kind: 'institution' });
      else await toLogin();
    });
    return () => {
      active = false;
    };
  }, [client, toLogin]);

  const loadInstitution = useCallback(() => client.getMyInstitution(), [client]);
  const sessionEnded = useCallback((expired: boolean) => void toLogin(expired ? loginTexts.expired : undefined), [toLogin]);

  return (
    <SafeAreaView style={styles.root}>
      {screen.kind === 'checking' ? <ActivityIndicator accessibilityLabel={loginTexts.loading} style={styles.root} /> : null}
      {screen.kind === 'login' ? (
        <LoginScreen
          login={(code, email, password) => client.login(code, email, password)}
          onLoggedIn={(institutionCode, email) => {
            void hints.write({ institutionCode, email });
            setScreen({ kind: 'institution' });
          }}
          notice={screen.notice}
          initialCode={screen.hint?.institutionCode}
          initialEmail={screen.hint?.email}
        />
      ) : null}
      {screen.kind === 'password' ? (
        <ChangePasswordScreen
          changePassword={(current, next, again) => client.changePassword(current, next, again)}
          onChanged={() => setScreen({ kind: 'institution', notice: passwordTexts.changed })}
          onCancel={() => setScreen({ kind: 'institution' })}
          onSessionEnded={() => sessionEnded(true)}
        />
      ) : null}
      {screen.kind === 'institution' ? (
        <InstitutionScreen
          loadInstitution={loadInstitution}
          notice={screen.notice}
          onSessionEnded={sessionEnded}
          onChangePassword={() => setScreen({ kind: 'password' })}
          onLogout={() => void client.logout().finally(() => void toLogin())}
        />
      ) : null}
      <StatusBar style="dark" />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#fff' },
});
