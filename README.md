# Hybrid Address Matcher

Smart multi-layer address validation system combining deterministic algorithms, Google Geocoding API, and AI-powered fallback logic.

## 🚀 Live Demo

- **Frontend:** [ccp-address-matcher.vercel.app](https://ccp-address-matcher.vercel.app)
- **Backend API:** Deployed on Azure App Service

## ✨ Key Features

✅ **Multi-Layer Validation** - 5 distinct validation methods with intelligent fallback  
✅ **Same Building Detection** - Correctly identifies addresses with different suite/apartment numbers  
✅ **High Accuracy** - 85-95% confidence for valid addresses using Google Geocoding  
✅ **AI Fallback** - GPT-4o-mini handles edge cases when traditional methods fail  
✅ **Production Ready** - Deployed with proper logging, error handling, and security  
✅ **Comprehensive Testing** - Full test suite with xUnit

## 🏗️ Architecture
```
┌─────────────────────────────────────────┐
│           React Frontend (Vercel)       │
│        TailwindCSS + Modern UI          │
└──────────────┬──────────────────────────┘
               │ HTTPS/CORS
               ↓
┌─────────────────────────────────────────┐
│      .NET 8 Web API (Azure)             │
├─────────────────────────────────────────┤
│  Layer 0: Local Normalization           │
│  Layer 1: USPS Validation (Mock)        │
│  Layer 2: Google Geocoding API          │
│  Layer 3: Place ID Matching             │
│  Layer 4: LLM Fallback (GPT-4o-mini)    │
└─────────────────────────────────────────┘
```

### Validation Layers

1. **Layer 0: Normalization** - Standardizes street types, state abbreviations, removes unit numbers
2. **Layer 1: USPS Validation** - CASS-certified address standardization (mock mode)
3. **Layer 2: Geocoding** - Google Maps API for precise geolocation
4. **Layer 3: Place ID** - Unique Google identifier matching
5. **Layer 4: AI Analysis** - GPT-4o-mini for complex edge cases

## 🛠️ Tech Stack

**Backend:**
- .NET 8.0 (C#)
- ASP.NET Core Web API
- Google Maps Geocoding API
- OpenAI GPT-4o-mini API
- xUnit for testing

**Frontend:**
- React 19
- Vite (build tool)
- TailwindCSS
- Vercel deployment

**Infrastructure:**
- Azure App Service (backend)
- Vercel (frontend)
- GitHub Actions (CI/CD)

## 📦 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 18+](https://nodejs.org/)
- Google Maps API key ([Get one here](https://console.cloud.google.com/))
- OpenAI API key ([Get one here](https://platform.openai.com/api-keys))

### Backend Setup

1. **Clone the repository**
```bash
git clone https://github.com/paureis/hybrid-address-matcher.git
cd hybrid-address-matcher
```

2. **Configure API keys using .NET User Secrets**
```bash
cd CCP.AddressMatcher
dotnet user-secrets init
dotnet user-secrets set "GoogleApiKey" "your-google-api-key"
dotnet user-secrets set "OpenAIApiKey" "your-openai-api-key"
```

3. **Run the backend**
```bash
dotnet run
```

The API will be available at `https://localhost:5001` and `http://localhost:5000`

4. **View API documentation**
```
http://localhost:5000/swagger
```

### Frontend Setup

1. **Navigate to frontend directory**
```bash
cd frontend
```

2. **Create environment file**
```bash
# Create .env file
echo "VITE_BACKEND_URL=http://localhost:5000" > .env
```

3. **Install dependencies and run**
```bash
npm install
npm run dev
```

The frontend will be available at `http://localhost:5173`

## 🧪 Running Tests
```bash
cd CCP.AddressMatcher.Tests
dotnet test
```

All tests should pass with 100% success rate.

## 🔒 Security

- API keys stored in Azure Key Vault (production) and User Secrets (local)
- CORS restricted to known frontend origins only
- No sensitive data logged
- Rate limiting on API calls
- `.gitignore` configured to prevent secret exposure

## 📊 API Endpoints

### Primary Endpoint

**POST** `/api/enhanced-compare` - Multi-layer address validation

**Request:**
```json
{
  "address1": "600 Montgomery St, San Francisco, CA 94111",
  "address2": "600 Montgomery Street Suite 1500, San Francisco, CA 94111"
}
```

**Response:**
```json
{
  "match": true,
  "confidence": 0.95,
  "method": "geocoding",
  "reason": "Identical Place ID",
  "layersUsed": ["Layer 0: Normalize", "Layer 2: Geocoding"],
  "distanceMeters": 0
}
```

### Additional Endpoints

- `/api/compare-addresses` - Local normalization only
- `/api/google-validate` - Google API validation
- `/api/geocoding-compare` - Geocoding-based comparison
- `/api/smart-compare` - Smart fallback logic
- `/api/enhanced-batch-compare` - Batch processing

See Swagger documentation for complete API reference.

## 🚀 Deployment

### Backend (Azure)

Automatic deployment via GitHub Actions on push to `main` branch.

**Environment variables configured in Azure:**
- `GoogleApiKey`
- `OpenAIApiKey`

### Frontend (Vercel)

Automatic deployment on push to `main` branch.

**Environment variable:**
- `VITE_BACKEND_URL` - Backend API URL

## 📈 Performance

- **API Response Time:** ~300-500ms (including external API calls)
- **Accuracy:** 85-95% match confidence for valid addresses
- **Caching:** 1-hour memory cache reduces redundant API calls
- **Rate Limiting:** Smart batching for bulk operations

## 🤝 Contributing

This is a portfolio project. Feedback and suggestions are always welcome!

## 📝 License

MIT License - See [LICENSE](LICENSE) file for details.

## 👤 Author

**Alvaro P Reis**
- Aspiring Cloud/Backend Developer
- GitHub: [@paureis](https://github.com/paureis)

---

**Built with the intention to solve real-world address matching challenges in healthcare operations**