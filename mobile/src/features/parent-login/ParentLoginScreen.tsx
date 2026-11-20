import { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, ActivityIndicator } from 'react-native';

export function ParentLoginScreen() {
  const [step, setStep] = useState<'phone' | 'sms' | 'kvkk'>('phone');
  const [phone, setPhone] = useState('');
  const [code, setCode] = useState('');
  const [accepted, setAccepted] = useState(false);
  const [loading, setLoading] = useState(false);

  const handlePhoneSubmit = async () => {
    setLoading(true);
    try {
      // Send SMS code
      setStep('sms');
    } finally {
      setLoading(false);
    }
  };

  const handleSmsSubmit = async () => {
    setLoading(true);
    try {
      // Verify SMS code
      setStep('kvkk');
    } finally {
      setLoading(false);
    }
  };

  const handleKvkkSubmit = async () => {
    setLoading(true);
    try {
      // Record consent & login
    } finally {
      setLoading(false);
    }
  };

  if (step === 'phone') {
    return (
      <View style={{ flex: 1, padding: 20, justifyContent: 'center' }}>
        <Text style={{ fontSize: 20, fontWeight: 'bold', marginBottom: 20 }}>Veli Girişi</Text>
        <TextInput
          placeholder="+905551234567"
          value={phone}
          onChangeText={setPhone}
          keyboardType="phone-pad"
          editable={!loading}
          style={{ borderWidth: 1, padding: 12, borderRadius: 8, marginBottom: 20 }}
        />
        <TouchableOpacity onPress={handlePhoneSubmit} disabled={loading} style={{ backgroundColor: '#2563eb', padding: 12, borderRadius: 8 }}>
          {loading ? <ActivityIndicator color="#fff" /> : <Text style={{ color: '#fff', textAlign: 'center', fontWeight: '600' }}>SMS Kodu Gönder</Text>}
        </TouchableOpacity>
      </View>
    );
  }

  if (step === 'sms') {
    return (
      <View style={{ flex: 1, padding: 20, justifyContent: 'center' }}>
        <Text style={{ fontSize: 20, fontWeight: 'bold', marginBottom: 20 }}>SMS Kodunu Girin</Text>
        <TextInput
          placeholder="123456"
          value={code}
          onChangeText={(t) => setCode(t.replace(/\D/g, '').slice(0, 6))}
          keyboardType="number-pad"
          maxLength={6}
          editable={!loading}
          style={{ borderWidth: 1, padding: 12, borderRadius: 8, marginBottom: 20, fontSize: 24, textAlign: 'center', letterSpacing: 8 }}
        />
        <TouchableOpacity onPress={handleSmsSubmit} disabled={loading || code.length !== 6} style={{ backgroundColor: '#2563eb', padding: 12, borderRadius: 8 }}>
          {loading ? <ActivityIndicator color="#fff" /> : <Text style={{ color: '#fff', textAlign: 'center', fontWeight: '600' }}>Doğrula</Text>}
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={{ flex: 1, padding: 20, justifyContent: 'center' }}>
      <Text style={{ fontSize: 20, fontWeight: 'bold', marginBottom: 20 }}>KVKK Aydınlatması</Text>
      <View style={{ backgroundColor: '#f3f4f6', padding: 12, borderRadius: 8, marginBottom: 20, maxHeight: 300 }}>
        <Text style={{ fontSize: 12, color: '#4b5563' }}>[Hukuk müşaviri tarafından hazırlanan KVKK metni burada görüntülenecek]</Text>
      </View>
      <TouchableOpacity onPress={() => setAccepted(!accepted)} style={{ flexDirection: 'row', alignItems: 'center', marginBottom: 20 }}>
        <View style={{ width: 20, height: 20, borderWidth: 1, borderRadius: 4, backgroundColor: accepted ? '#2563eb' : '#fff', marginRight: 12 }} />
        <Text style={{ fontSize: 14 }}>Verilerimin işlenmesine rıza veriyorum</Text>
      </TouchableOpacity>
      <TouchableOpacity onPress={handleKvkkSubmit} disabled={loading || !accepted} style={{ backgroundColor: '#2563eb', padding: 12, borderRadius: 8 }}>
        {loading ? <ActivityIndicator color="#fff" /> : <Text style={{ color: '#fff', textAlign: 'center', fontWeight: '600' }}>Devam</Text>}
      </TouchableOpacity>
    </View>
  );
}
