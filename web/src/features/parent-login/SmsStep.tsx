import { useState } from 'react';

export function SmsStep({ onSubmit }: { onSubmit: (code: string) => void }) {
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      onSubmit(code);
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div>
        <label htmlFor="code" className="block text-sm font-medium">
          SMS Kodu
        </label>
        <input
          id="code"
          type="text"
          value={code}
          onChange={(e) => setCode(e.target.value.replace(/\D/g, '').slice(0, 6))}
          placeholder="123456"
          inputMode="numeric"
          maxLength={6}
          disabled={loading}
          className="mt-1 block w-full px-3 py-2 border rounded-md text-center text-2xl tracking-widest"
          required
        />
      </div>
      <button
        type="submit"
        disabled={loading || code.length !== 6}
        className="w-full px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
      >
        {loading ? 'Doğrulanıyor...' : 'Doğrula'}
      </button>
    </form>
  );
}
