import { useState } from 'react';

export function KvkkStep({ onSubmit }: { onSubmit: (accepted: boolean) => void }) {
  const [accepted, setAccepted] = useState(false);
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    try {
      onSubmit(accepted);
    } finally {
      setLoading(false);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <div className="bg-gray-50 p-4 rounded-md max-h-96 overflow-y-auto">
        <h3 className="font-semibold mb-2">KVKK Aydınlatması</h3>
        <p className="text-sm text-gray-700">
          [Hukuk müşaviri tarafından hazırlanan KVKK metni burada görüntülenecek]
        </p>
      </div>
      <label className="flex items-center">
        <input
          type="checkbox"
          checked={accepted}
          onChange={(e) => setAccepted(e.target.checked)}
          disabled={loading}
          className="rounded"
          required
        />
        <span className="ml-2 text-sm">Verilerimin işlenmesine rıza veriyorum</span>
      </label>
      <button
        type="submit"
        disabled={loading || !accepted}
        className="w-full px-4 py-2 bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50"
      >
        {loading ? 'İşleniyor...' : 'Devam'}
      </button>
    </form>
  );
}
