import { Routes, Route, Navigate } from 'react-router-dom'
import Login from './pages/Login'
import Expedientes from './pages/Expedientes'
import DetalleExpediente from './pages/DetalleExpediente'
import NuevoExpediente from './pages/NuevoExpediente'
import EditarExpediente from './pages/EditarExpediente'
import RutaProtegida from './components/RutaProtegida'

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/login" />} />
      <Route path="/login" element={<Login />} />
      <Route path="/expedientes" element={
        <RutaProtegida><Expedientes /></RutaProtegida>
      } />
      <Route path="/expedientes/nuevo" element={
        <RutaProtegida><NuevoExpediente /></RutaProtegida>
      } />
      <Route path="/expedientes/:id/editar" element={
        <RutaProtegida><EditarExpediente /></RutaProtegida>
      } />
      <Route path="/expedientes/:id" element={
        <RutaProtegida><DetalleExpediente /></RutaProtegida>
      } />
    </Routes>
  )
}

export default App