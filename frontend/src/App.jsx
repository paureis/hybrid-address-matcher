// src/App.jsx - Fixed for enhanced-compare endpoint

import React, { useState } from 'react';

function App() {
  const [address1, setAddress1] = useState('');
  const [address2, setAddress2] = useState('');
  const [result, setResult] = useState(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  // ✅ Correct hardcoded Azure URL with https
  const backendUrl = 'https://ccp-address-matcher-backend-a9eze9fmf8dvccga.eastus-01.azurewebsites.net';
  console.log('Backend URL in use:', backendUrl);

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setResult(null);

    try {
      // 🎯 FIXED: Using enhanced-compare for single comparison
      const response = await fetch(`${backendUrl}/api/enhanced-compare`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ 
          address1: address1, 
          address2: address2 
        }),
      });

      if (!response.ok) {
        const text = await response.text();
        console.error('API Error Response:', text);
        throw new Error(`API request failed with status ${response.status}`);
      }

      const data = await response.json();
      console.log('Enhanced Compare Response:', data);
      setResult(data);
    } catch (err) {
      console.error('Enhanced comparison error:', err);
      setError('An error occurred while comparing the addresses. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex flex-col items-center justify-center min-h-screen bg-gray-900 text-white p-4">
      <h1 className="text-3xl font-bold mb-6">Enhanced Address Matching</h1>
      <p className="text-gray-400 mb-6 text-center">
        5-Layer AI-Powered Address Validation System
      </p>

      <form onSubmit={handleSubmit} className="bg-gray-800 p-6 rounded shadow-md w-full max-w-md space-y-4">
        <div>
          <label htmlFor="address1" className="block mb-1 text-left font-semibold">Address 1:</label>
          <input
            id="address1"
            type="text"
            value={address1}
            onChange={(e) => setAddress1(e.target.value)}
            placeholder="e.g., 600 Montgomery St, San Francisco, CA 94111"
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
            placeholder="e.g., 600 Montgomery Street Suite 1500, San Francisco, CA 94111"
            className="w-full p-2 rounded bg-gray-700 border border-gray-600 text-white focus:outline-none focus:ring"
            required
          />
        </div>

        <button
          type="submit"
          disabled={loading}
          className="w-full p-2 bg-blue-600 hover:bg-blue-700 rounded font-semibold disabled:opacity-50"
        >
          {loading ? 'Analyzing...' : 'Compare Addresses'}
        </button>
      </form>

      <div className="mt-6 bg-gray-800 p-4 rounded shadow-md w-full max-w-md">
        <h2 className="text-xl font-semibold mb-2">Results</h2>

        {error && <p className="text-red-400">{error}</p>}

        {result && (
          <div className="space-y-3">
            {/* Match result with confidence */}
            <div className="border-b border-gray-600 pb-2">
              <span className="font-semibold">Match Result:</span>
              <p className={result.match ? 'text-green-400 text-lg font-bold' : 'text-red-400 text-lg font-bold'}>
                {result.match ? '✅ Addresses Match' : '❌ Different Addresses'}
              </p>
              {result.confidence && (
                <p className="text-gray-400 text-sm">
                  Confidence: {Math.round(result.confidence * 100)}%
                </p>
              )}
            </div>

            {/* Method and layers used */}
            <div>
              <span className="font-semibold">Method Used:</span>
              <p className="text-blue-400 capitalize">{result.method}</p>
              
              {result.layersUsed && (
                <div className="mt-1">
                  <span className="text-sm font-semibold">Layers Processed:</span>
                  <ul className="text-sm text-gray-300 ml-4">
                    {result.layersUsed.map((layer, index) => (
                      <li key={index} className="list-disc">{layer}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>

            {/* Show reason */}
            {result.reason && (
              <div>
                <span className="font-semibold">Analysis:</span>
                <p className="text-yellow-300 text-sm">{result.reason}</p>
              </div>
            )}

            {/* Show geocoded addresses if available */}
            {result.rawAddresses && (
              <div>
                <span className="font-semibold">Original Addresses:</span>
                <div className="text-sm text-gray-300 mt-1">
                  <p><strong>1:</strong> {result.rawAddresses.address1}</p>
                  <p><strong>2:</strong> {result.rawAddresses.address2}</p>
                </div>
              </div>
            )}

            {/* Show distance if available */}
            {result.distanceMeters !== undefined && result.distanceMeters !== null && (
              <div>
                <span className="font-semibold">Distance:</span>
                <p className="text-blue-400">
                  {result.distanceMeters === 0 ? 'Same location' : `${result.distanceMeters}m apart`}
                </p>
              </div>
            )}

            {/* Performance stats */}
            <div className="mt-4 pt-2 border-t border-gray-600">
              <p className="text-xs text-gray-500">
                Enhanced 5-Layer Validation System: Normalization → USPS → Geocoding → Place ID → AI Analysis
              </p>
            </div>
          </div>
        )}

        {!error && !result && (
          <div className="text-center">
            <p className="text-gray-400 italic mb-2">Enhanced comparison results will appear here</p>
            <div className="text-xs text-gray-500">
              <p>🔍 Layer 1: Address Normalization</p>
              <p>📮 Layer 2: USPS Validation</p>
              <p>🌍 Layer 3: Google Geocoding</p>
              <p>📍 Layer 4: Place ID Matching</p>
              <p>🤖 Layer 5: AI Analysis</p>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default App;