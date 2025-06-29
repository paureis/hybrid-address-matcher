# ccp-address-matcher

# Hybrid Deterministic + LLM Address Matcher

A **hybrid deterministic address normalization and matching system** using:
- **.NET 8 (C#) API on Azure App Services** for backend normalization.
- **React + Vite frontend hosted on Vercel** for user-friendly testing.
- **REST API design** with clean separation of frontend/backend for real-world deployment experience.

## 🚀 Live Demo

- **Frontend (Vercel):** [ccp-address-matcher.vercel.app](https://ccp-address-matcher-byuc20ex5-pau-reis-projects.vercel.app/)

## 🛠️ Features

✅ Input two addresses for comparison.  
✅ Normalizes addresses (expands abbreviations, extracts house number, city, state, zip reliably).  
✅ Returns a match result and differences if addresses differ.  
✅ Fully deployed on **Azure (API)** and **Vercel (frontend)**.  
✅ CORS handled for cross-platform deployment testing.

## 🏗️ Tech Stack

- **Frontend:** React, Vite, TailwindCSS, hosted on Vercel
- **Backend:** .NET 8, C#, Azure App Services
- **Tools:** Thunder Client (testing), Git, GitHub Actions (optional)

## 🗂️ Project Structure

- `/frontend`: React + Vite frontend
- `/CCP.AddressMatcher`: .NET 8 backend API
- `.env`: Stores environment variables for frontend backend URL

## 🚦 API Endpoints

- `POST /api/hybrid-compare` – Accepts `{ address1, address2 }` JSON and returns normalized comparison results.

### Backend:
```bash
cd CCP.AddressMatcher
dotnet build
dotnet run
