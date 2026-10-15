namespace DespachoJuridico.API.Services;

// DJ-129: armazón visual compartido de los correos de acuerdo (encabezado navy
// con subtítulo de acento, tarjeta de datos, recuadro de síntesis, botón y
// firma). Antes cada correo pegaba su propia copia de este CSS -- un cambio de
// estilo había que repetirlo a mano en cada plantilla. Hoy lo usan el correo de
// acuerdo detectado (ScraperAcuerdosService) y el de registro manual
// (CorreoAcuerdoManual); las demás plantillas (recordatorios, asignación,
// vencimientos) todavía tienen su copia y pueden migrar aquí después.
//
// Todo texto que venga de un usuario o de ADISON debe llegar ya codificado con
// WebUtility.HtmlEncode -- esta clase solo ensambla, no escapa.
internal static class PlantillaCorreo
{
    internal const string Estilos = @"
  body{font-family:Georgia,'Times New Roman',serif;background:#f7f5f0;margin:0;padding:0;}
  .container{max-width:580px;margin:40px auto;background:#ffffff;border-radius:8px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,0.08);}
  .header{background:#1c2b4a;padding:32px 40px;text-align:center;}
  .header h1{color:#ffffff;font-size:20px;margin:0;font-weight:normal;letter-spacing:1px;}
  .header p{color:#9a7c3c;font-size:13px;margin:6px 0 0;letter-spacing:2px;text-transform:uppercase;}
  .body{padding:40px;color:#333333;}
  .body p{font-size:15px;line-height:1.7;margin:0 0 16px;}
  .highlight{background:#f0f4fa;border-left:4px solid #9a7c3c;padding:16px 20px;margin:24px 0;border-radius:0 6px 6px 0;}
  .highlight p{margin:4px 0;font-size:14px;color:#444;}
  .highlight strong{color:#1c2b4a;}
  .sintesis{background:#fffdf7;border:1px solid #e0ddd6;padding:16px 20px;margin:16px 0;border-radius:6px;font-size:14px;line-height:1.6;color:#555;}
  .aviso{background:#f7f5f0;border:1px solid #e0ddd6;padding:12px 16px;margin:16px 0;border-radius:6px;font-size:13px;line-height:1.6;color:#555;}
  .cta{text-align:center;margin:28px 0 8px;}
  .cta a{display:inline-block;background:#1c2b4a;color:#ffffff;padding:12px 28px;border-radius:6px;text-decoration:none;font-size:14px;font-weight:500;}
  .footer{background:#f7f5f0;padding:20px 40px;text-align:center;border-top:1px solid #e0ddd6;}
  .footer p{font-size:12px;color:#888;margin:0;}
  @media (max-width:600px){
    .container{margin:0;border-radius:0;}
    .header{padding:24px 20px;}
    .body{padding:24px 20px;}
    .footer{padding:16px 20px;}
  }";

    // `subtituloHtml` y `contenidoHtml` ya vienen codificados/armados por quien llama.
    internal static string Armar(string subtituloHtml, string contenidoHtml) => $@"
<!DOCTYPE html><html lang='es'><head><meta charset='UTF-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<style>{Estilos}
</style></head>
<body><div class='container'>
  <div class='header'>
    <h1>Despacho Jurídico Acedo e Hijos</h1>
    <p>{subtituloHtml}</p>
  </div>
  <div class='body'>
{contenidoHtml}
    <p>Atentamente,<br><strong>Despacho Jurídico Acedo e Hijos</strong></p>
  </div>
  <div class='footer'>
    <p>Este es un mensaje automático del Sistema de Gestión de Expedientes.</p>
    <p>Por favor no responda a este correo.</p>
  </div>
</div></body></html>";
}
