// src/App.jsx - Clean, deploy-ready Hybrid Address Matching UI

import React, { useState } from 'react';

function App() {
  const [address1, setAddress1] = useState('');
  const [address2, setAddress2] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  // Use environment variable for backend URL with fallback to localhost
  const backendUrl = import.meta.env.VITE_BACKEND_URL || 'http://localhost:5001';
  console.log('Backend URL in use:', backendUrl);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setResult(null);

    try {
      const response = await fetch(`${backendUrl}/api/hybrid-compare`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ address1, address2 }),
      });

      if (!response.ok) {
        const text = await response.text();
        console.error('API Error Response:', text);
        throw new Error(`API request failed with status ${response.status}`);
      }

      const data = await response.json();
      console.log('Hybrid Compare Response:', data);
      setResult(data);
    } catch (err) {
      console.error('Hybrid comparison error:', err);
      setError('An error occurred while comparing the addresses. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gray-900 text-white p-4">
      <h1 className="text-3xl font-bold mb-6">Hybrid Address Matching</h1>

      <form onSubmit={handleSubmit} className="bg-gray-800 p-6 rounded shadow-md w-full max-w-md space-y-4">
        <div>
          <label htmlFor="address1" className="block mb-1 text-left font-semibold">Address 1:</label>
          <input
            id="address1"
            type="text"
            value={address1}
            onChange={(e) => setAddress1(e.target.value)}
            placeholder="e.g., 123 Main St, City, State, ZIP"
            className="w-full p-2 rounded bg-gray-700 border border-gray-600 text-white focus:outline-none focus:ring"
            required
          />
        </div>

        <div>
          <label htmlFor="address2" className="block mb-1 text-left font-semibold">Address 2:</label>
          <input
            id="address2"
            type="text"
            value={address2}
            onChange={(e) => setAddress2(e.target.value)}
            placeholder="e.g., 123 Main St, City, State, ZIP"
            className="w-full p-2 rounded bg-gray-700 border border-gray-600 text-white focus:outline-none focus:ring"
            required
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full p-2 bg-blue-600 hover:bg-blue-700 rounded font-semibold disabled:opacity-50"
        >
          {loading ? 'Comparing...' : 'Compare Addresses'}
        </button>
      </form>

      <div className="mt-6 bg-gray-800 p-4 rounded shadow-md w-full max-w-md">
        <h2 className="text-xl font-semibold mb-2">Results</h2>

        {error && <p className="text-red-400">{error}</p>}

        {result && (
          <div className="space-y-2">
            <div>
              <span className="font-semibold">Normalized Address 1:</span>
              <p className="text-green-400 break-words">{result.normalizedAddress1}</p>
            </div>

            <div>
              <span className="font-semibold">Normalized Address 2:</span>
              <p className="text-green-400 break-words">{result.normalizedAddress2}</p>
            </div>

            <div>
              <span className="font-semibold">Match Result:</span>
              <p className={result.match ? 'text-green-400' : 'text-red-400'}>
                {result.match ? '✅ Addresses match' : '❌ Addresses do not match'}
              </p>
            </div>

            {result.differences && result.differences.length > 0 && (
              <div>
                <span className="font-semibold">Differences:</span>
                <ul className="list-disc list-inside text-yellow-300">
                  {result.differences.map((diff, index) => (
                    <li key={index}>{diff}</li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        )}

        {!error && !result && (
          <p className="text-gray-400 italic">Comparison results will appear here after submission.</p>
        )}
      </div>
    </div>
  );
}

export default App;
