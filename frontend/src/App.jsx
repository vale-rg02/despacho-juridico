import { Routes, Route, Navigate } from 'react-router-dom'
import Login from './pages/Login'
import Expedientes from './pages/Expedientes'
import DetalleExpediente from './pages/DetalleExpediente'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" />} />
      <Route path="/login" element={<Login />} />
      <Route path="/expedientes" element={<Expedientes />} />
      <Route path="/expedientes/:id" element={<DetalleExpediente />} />
    </Routes>
  )
}

export default App