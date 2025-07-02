CCP Address Matcher
Smart Geocoding-Based Address Matching System with intelligent fallback to local normalization.
A robust address comparison system that uses Google Geocoding API for high-accuracy matching, with seamless fallback to deterministic local normalization when needed.

🚀 Live Demo

Frontend (Vercel): ccp-address-matcher.vercel.app
Backend API (Azure): https://ccp-address-matcher-backend-a9eze9fmf8dvccga.eastus-01.azurewebsites.net

✨ Key Features
✅ Smart Geocoding - Uses Google Maps API for precise address matching
✅ Same Building Detection - Correctly identifies addresses with different suite/apartment numbers
✅ Intelligent Fallback - Falls back to local normalization if geocoding fails
✅ Confidence Scoring - Provides match confidence percentages
✅ Multiple Comparison Methods - Place ID, formatted address, components, and distance-based matching
✅ International Support - Works with addresses worldwide
✅ Production Ready - Deployed on Azure App Services with proper error handling
🎯 Problem Solved

Traditional string-based address matching fails on common variations:

❌ 17399 SW 54th St, Miramar, FL vs 17399 SW 54th St Suite 205, Miramar, FL
❌ 1600 Pennsylvania Ave NW vs 1600 Pennsylvania Avenue Northwest
❌ 17399 Sw 54th St, Miramar vs 17399 Sw 54th St, Miramar, FL

Our system correctly identifies these as same building/location with high confidence.
🏗️ Tech Stack
Frontend:

React 18 + Vite
TailwindCSS for styling
Deployed on Vercel

Backend:

.NET 8 (C#) Web API
Google Maps Geocoding API integration
Memory caching for API optimization
Deployed on Azure App Services

Tools:

Thunder Client for API testing
Azure Portal for cloud management
Google Cloud Console for API management

🚦 API Endpoints
Smart Comparison (Recommended)
httpPOST /api/smart-compare
Content-Type: application/json

🛠️ Local Development
Prerequisites

.NET 8 SDK
Node.js 18+
Google Maps API key with Geocoding API enabled

Backend Setup
bashcd CCP.AddressMatcher
dotnet restore
dotnet run
Frontend Setup
bashcd frontend
npm install
npm run dev
Environment Configuration
Backend (appsettings.json):
json{
  "GoogleApiKey": "your-google-api-key-here"
}
Frontend (.env):
envVITE_BACKEND_URL=http://localhost:5000

🔧 Configuration
Google API Setup

Go to Google Cloud Console
Enable the Geocoding API
Create an API key
Add the key to your backend configuration

Azure Deployment

Create an Azure App Service
Add GoogleApiKey to Application Settings
Deploy the .NET application

Vercel Deployment

Connect your GitHub repository
Set VITE_BACKEND_URL environment variable
Deploy automatically on push

📊 Performance & Accuracy

Geocoding Accuracy: 85-95% confidence for valid addresses
API Response Time: ~300-500ms (including Google API calls)
Caching: 1-hour memory cache reduces API costs
Fallback Success: Local normalization handles edge cases
Cost Optimization: Smart caching + batch processing

🤝 Contributing

Fork the repository
Create a feature branch
Make your changes
Test with Thunder Client or similar
Submit a pull request

📝 License
This project is licensed under the MIT License.
🔗 Links
Live Demo: ccp-address-matcher.vercel.app
API Documentation: Available via Swagger when running locally
Google Geocoding API: Documentation
