# 🐊 CrocManager

**Interfaz gráfica para [croc](https://github.com/schollz/croc)** — Transfiere archivos de forma segura entre computadores.

![Licencia](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-9.0-512BD4)
![WebDesktop](https://img.shields.io/badge/WebDesktop-1.0-38bdf8)

CrocManager envuelve la herramienta CLI `croc` en una aplicación de escritorio Windows con interfaz moderna, usando **WebDesktop** (.NET + WebView2) y HTML/CSS/JS.

---

## ✨ Características

| Característica | Descripción |
|---|---|
| 🚀 **Enviar archivos** | Selección múltiple de archivos y carpetas |
| 📝 **Enviar texto** | Envía URLs, mensajes cortos o cualquier texto |
| 📥 **Recibir archivos** | Ingresa el código y selecciona carpeta destino |
| 🔒 **Cifrado E2E** | Usa PAKE (Password-Authenticated Key Exchange) |
| 📊 **Progreso en tiempo real** | Barra de progreso con porcentaje, velocidad y ETA |
| 📜 **Historial** | Registro local de todas las transferencias |
| 🎨 **Tema oscuro** | Interfaz moderna tipo Tailwind |
| 📋 **Copiar código** | Un clic copia el code phrase al portapapeles |

---

## 📦 Requisitos

- **Windows 10 o superior** (o Windows Server 2019+)
- **[WebView2 Runtime](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)** (Evergreen Runtime)
- **[.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)** o superior
- **croc CLI** (se instala automáticamente vía winget o manualmente)

---

## 🚀 Instalación

### 1. Instalar croc

```powershell
winget install schollz.croc
```

O alternativamente:

```powershell
choco install croc
scoop install croc
```

Verifica la instalación:

```bash
croc --version
# → croc version v10.4.13
```

### 2. Clonar y compilar

```bash
git clone https://github.com/geomorillo/crocmanager
cd crocmanager
dotnet build
```

### 3. Ejecutar

```bash
dotnet run
```

O ejecuta directamente el binario:

```bash
.\bin\Debug\net9.0-windows\CrocManager.exe
```

---

## 🎮 Uso

### Enviar archivos

1. Abre CrocManager
2. Ve a la pestaña **Enviar**
3. Haz clic en **📁 Examinar** para seleccionar archivos, o **📂 Carpeta** para una carpeta
4. Opcional: ingresa un **código personalizado** (mín. 6 caracteres)
5. Haz clic en **🚀 Enviar**
6. Comparte el código generado con la otra persona

### Recibir archivos

1. Ve a la pestaña **Recibir**
2. Ingresa el código que te compartieron
3. Selecciona la **carpeta de destino**
4. Haz clic en **📥 Recibir**
5. ¡Los archivos se descargan automáticamente!

### Enviar texto

1. Ve a la pestaña **Texto**
2. Escribe o pega el texto
3. Opcional: código personalizado
4. Haz clic en **📤 Enviar texto**

### Historial

- Ve a la pestaña **Historial** para ver todas las transferencias
- Muestra tipo, código, archivos, estado y tiempo
- Usa **🗑️ Limpiar** para borrar el historial

---

## 🏗️ Estructura del proyecto

```
crocmanager/
├── CrocManager.csproj          # Proyecto .NET 9 + WebView2
├── Program.cs                  # Entry point, configura WebWindow
├── lib/
│   └── WebDesktop.Core.dll     # WebDesktop framework (DLL compilada)
├── Models/
│   └── HistoryEntry.cs         # Modelo de datos del historial
├── Services/
│   ├── CrocService.cs          # Wrapper de croc CLI + progreso real
│   └── HistoryService.cs       # Persistencia del historial (JSON)
└── wwwroot/
    ├── index.html              # UI principal (4 tabs)
    ├── css/
    │   └── style.css           # Tema oscuro
    └── js/
        └── app.js              # Lógica frontend
```

---

## 🧠 Arquitectura

```
┌─────────────────────────────────────────────────┐
│                 CrocManager                      │
│  ┌──────────┐    ┌────────────────────────────┐ │
│  │ Frontend │◄──►│   WebDesktop Bridge        │ │
│  │ HTML/JS  │    │   (invoke / ExecuteScript) │ │
│  └──────────┘    └──────────┬─────────────────┘ │
│                             │                    │
│                    ┌────────▼─────────┐          │
│                    │  CrocService.cs  │          │
│                    │  (C#)            │          │
│                    └────────┬─────────┘          │
│                             │                    │
│                    ┌────────▼─────────┐          │
│                    │   croc CLI       │          │
│                    │   (proceso ext.) │          │
│                    └──────────────────┘          │
└─────────────────────────────────────────────────┘
```

### Comunicación en tiempo real

El progreso de las transferencias se transmite en vivo desde C# a JavaScript usando `ExecuteScriptAsync`, lo que permite actualizar la barra de progreso con datos reales de croc sin polling.

---

## ⚙️ Dependencias

| Paquete | Versión | Propósito |
|---|---|---|
| [Microsoft.Web.WebView2](https://www.nuget.org/packages/Microsoft.Web.WebView2) | 1.0.3065.39 | Motor Chromium para la UI |
| [WebDesktop.Core](https://github.com/geomorillo/webdesktop) | 1.0.0 | Framework puente C# ↔ JS |
| [croc](https://github.com/schollz/croc) | v10.4.13+ | CLI de transferencia de archivos |

---

## 📄 Licencia

MIT © 2026 **Manuel Jhobanny Morillo Ordoñez**

---

## 🙏 Créditos

- [schollz/croc](https://github.com/schollz/croc) — La increíble herramienta CLI que hace posible todo esto
- [WebDesktop](https://github.com/geomorillo/webdesktop) — Framework .NET para apps desktop con web technologies
