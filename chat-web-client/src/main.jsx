import React from 'react'
import ReactDOM from 'react-dom/client'
import App from './App.jsx'
import './App.css'
import { GoogleOAuthProvider } from '@react-oauth/google'

// Chìa khóa Google của bác đây!
const CLIENT_ID = "923986382212-fo785k6kaingoj9odjsvanodvj2pvv6e.apps.googleusercontent.com";

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    {/* Bọc toàn bộ App bằng Khiên Xác Thực Google */}
    <GoogleOAuthProvider clientId={CLIENT_ID}>
      <App />
    </GoogleOAuthProvider>
  </React.StrictMode>,
)
