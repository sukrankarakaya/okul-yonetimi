import { createOkulClient, type InstitutionResult } from '@okul/api-client';
import { StatusBar } from 'expo-status-bar';
import { SafeAreaView, StyleSheet } from 'react-native';
import { InstitutionScreen } from './src/features/institution/InstitutionScreen';

const baseUrl = process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5080';

// Access token in memory only (docs/security.md — SecureStore arrives with the real login, spec 0002).
let accessToken: string | null = null;
const client = createOkulClient({ baseUrl, getToken: () => accessToken });

async function loadInstitution(): Promise<InstitutionResult> {
  if (__DEV__ && accessToken === null) {
    const { devLogin } = await import('./src/auth/devLogin');
    accessToken = await devLogin(baseUrl);
  }
  return client.getMyInstitution();
}

export default function App() {
  return (
    <SafeAreaView style={styles.root}>
      <InstitutionScreen loadInstitution={loadInstitution} />
      <StatusBar style="dark" />
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#fff' },
});
