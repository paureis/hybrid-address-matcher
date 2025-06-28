import React, { useState } from "react";

function App() {
  const [address, setAddress] = useState("");
  const [validatedAddress, setValidatedAddress] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError("");
    setValidatedAddress("");

    try {
      const response = await fetch("https://localhost:5001/api/google-validate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ address }),
      });

      if (!response.ok) {
        throw new Error("API request failed");
      }

      const data = await response.json();
      setValidatedAddress(data.formatted);
    } catch (err) {
      console.error(err);
      setError("An error occurred while validating the address.");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-900 flex flex-col items-center p-6 text-white">
      {/* Header */}
      <header className="mb-10">
        <h1 className="text-4xl font-bold">Hybrid Address Matching</h1>
      </header>

      {/* Address Input Form */}
      <form
        onSubmit={handleSubmit}
        className="w-full max-w-md bg-gray-800 p-6 rounded shadow-md"
      >
        <label htmlFor="address" className="block mb-2 font-semibold">
          Enter an address:
        </label>
        <input
          id="address"
          type="text"
          value={address}
          onChange={(e) => setAddress(e.target.value)}
          placeholder="e.g. 123 Main St, Springfield, IL"
          className="w-full px-4 py-2 rounded bg-gray-700 text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        <button
          type="submit"
          disabled={loading}
          className={`mt-4 w-full py-2 rounded transition ${
            loading
              ? "bg-blue-400 cursor-not-allowed"
              : "bg-blue-600 hover:bg-blue-700"
          }`}
        >
          {loading ? "Validating..." : "Submit"}
        </button>
      </form>

      {/* Results Section */}
      <section className="w-full max-w-md mt-8 bg-gray-800 p-6 rounded shadow-md">
        <h2 className="text-xl font-semibold mb-4">Results</h2>
        {loading && <p className="text-gray-400 italic">Loading...</p>}
        {error && <p className="text-red-400">{error}</p>}
        {validatedAddress && (
          <p>
            Validated Address:{" "}
            <strong className="text-green-400">{validatedAddress}</strong>
          </p>
        )}
        {!loading && !validatedAddress && !error && (
          <p className="text-gray-400 italic">
            Results will appear here after submission.
          </p>
        )}
      </section>
    </div>
  );
}

export default App;
