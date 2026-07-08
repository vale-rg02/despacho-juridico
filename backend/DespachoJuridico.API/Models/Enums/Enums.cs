namespace DespachoJuridico.API.Models.Enums;

public enum Prioridad { Normal, Prioritario, Urgente }
public enum EstadoExpediente { Abierto, Cerrado, Pausado }
public enum RolUsuario { Litigante, Socio }
public enum CanalNotificacion { Email, WhatsApp, Sistema }

public enum NivelAcceso
{
    Estandar = 0,
    Administrativo = 1,
    Superior = 2
}