import fs from "node:fs/promises";
import path from "node:path";
import { pathToFileURL } from "node:url";
import { Presentation, PresentationFile } from "@oai/artifact-tool";

const workspaceDir = "C:/Users/elton/source/repos/EltonYaelAlbaRoldan/LogiApp";
const SKILL_DIR = "C:/Users/elton/.codex/plugins/cache/openai-primary-runtime/presentations/26.905.11957/skills/presentations";
const TMP_DIR = path.join(workspaceDir, ".tmp/build-presentacion-octubre");
const FINAL_PPTX = path.join(workspaceDir, "Entregables/LogiApp_Estado_Tecnico_Octubre_2026.pptx");
process.env.RUNTIME_NODE_MODULES = "C:/Users/elton/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules";
const { resolvePresentationFont, finalizePresentation } = await import(
  pathToFileURL(path.join(SKILL_DIR, "container_tools/artifact_tool_utils.mjs")).href,
);
const fontFamily = resolvePresentationFont();
const presentation = Presentation.create({ slideSize: { width: 1280, height: 720 } });
const C = { blue: "#1976B9", cyan: "#56C5E8", ink: "#111827", muted: "#5D6878", pale: "#EFF5F9", white: "#FFFFFF", green: "#17856A", amber: "#B7791F" };

function textbox(slide, name, text, x, y, w, h, size, color = C.ink, bold = false) {
  const box = slide.shapes.add({ geometry: "textbox", name, position: { left: x, top: y, width: w, height: h }, fill: "none", line: { fill: "none", width: 0 } });
  box.text = text;
  box.text.style = { typeface: fontFamily, fontSize: size, bold, color, autoFit: "shrinkText", verticalAlignment: "middle" };
  return box;
}

function makeSlide(number, kicker, title, notes) {
  const slide = presentation.slides.add();
  slide.background.fill = C.white;
  slide.shapes.add({ geometry: "rect", name: "brand-stripe", position: { left: 0, top: 0, width: 34, height: 720 }, fill: C.blue, line: { fill: C.blue, width: 0 } });
  textbox(slide, "brand", "LOGIAPP", 82, 36, 250, 32, 18, C.blue, true);
  if (kicker) textbox(slide, "section", kicker.toUpperCase(), 82, 95, 640, 25, 14, C.blue, true);
  textbox(slide, "title", title, 82, 130, 1120, 78, 36, C.ink, true);
  textbox(slide, "page", String(number).padStart(2, "0"), 1172, 665, 50, 25, 13, C.muted, true);
  slide.speakerNotes.textFrame.setText(notes);
  return slide;
}

function label(slide, text, x, y, w, color = C.blue) { textbox(slide, `label-${text}`, text.toUpperCase(), x, y, w, 26, 15, color, true); }

// 1 — cover
{
  const s = presentation.slides.add(); s.background.fill = C.white;
  s.shapes.add({ geometry: "rect", name: "brand-stripe", position: { left: 0, top: 0, width: 34, height: 720 }, fill: C.blue, line: { fill: C.blue, width: 0 } });
  textbox(s, "brand", "LOGIAPP", 82, 46, 280, 36, 20, C.blue, true);
  textbox(s, "cover-title", "Estado técnico\ny siguientes pasos", 82, 185, 940, 176, 52, C.ink, true);
  textbox(s, "cover-subtitle", "Base disponible para una nueva versión", 86, 398, 850, 60, 27, C.muted);
  s.shapes.add({ geometry: "rect", name: "accent-line", position: { left: 86, top: 520, width: 380, height: 5 }, fill: C.cyan, line: { fill: C.cyan, width: 0 } });
  textbox(s, "period", "Revisión para octubre de 2026", 86, 550, 700, 35, 18, "#123B5D", true);
  s.speakerNotes.textFrame.setText("Esta presentación resume capacidades verificables de LogiApp v2.2.1 y propone cómo validar su comportamiento. El historial del repositorio llega al 9 de septiembre de 2026; no se atribuyen cambios de código posteriores ni métricas de rendimiento no medidas.");
}

// 2 — what exists
{
  const s = makeSlide(2, "Estado de la versión", "La versión 2.2.1 ya cubre el ciclo del viaje", "La versión 2.2.1 quedó registrada el 9 de septiembre de 2026. Incluye carga de combustible durante el viaje, seguimiento en segundo plano, notificación de seguimiento activo, persistencia local sin conexión e historial con acceso a evidencias. Fuentes: commit b48f123; Vistas/PaginaTerminarViaje.xaml.cs; Servicios/SincronizadorGlobal.cs; Platforms/Android/ServicioUbicacionViaje.cs.");
  textbox(s, "intro", "El trabajo reciente se concentró en procesos internos que sostienen el registro y seguimiento.", 88, 229, 1080, 62, 23, C.muted);
  const items = [
    ["Seguimiento", "Registro periódico de ubicación mientras el viaje está activo."],
    ["Continuidad", "Datos pendientes guardados localmente y sincronizados al volver la conexión."],
    ["Cierre", "Recargas de combustible y evidencias incorporadas al cierre del viaje."],
  ];
  const xs = [88, 465, 842];
  items.forEach(([head, body], i) => { label(s, `0${i + 1}  ${head}`, xs[i], 360, 320); textbox(s, `body-${i}`, body, xs[i], 403, 310, 126, 22, C.ink); });
  textbox(s, "version-note", "Referencia: versión 2.2.1 · 9 sep 2026", 88, 594, 850, 28, 16, C.muted);
}

// 3 — invisible operational work
{
  const s = makeSlide(3, "Continuidad operativa", "El seguimiento puede continuar en segundo plano", "En Android se agregó un servicio de ubicación en segundo plano con notificación persistente y permisos para mantener el seguimiento. El código contempla puntos de ubicación periódicos y guardado local antes de sincronizar. El teléfono debe permanecer encendido, con ubicación activada y sin forzar el cierre de la app. Fuentes: Platforms/Android/ServicioUbicacionViaje.cs; Servicios/ControlTrackingSegundoPlano.cs; commit 0244ae5 y b48f123.");
  textbox(s, "explanation", "El operador puede cambiar de pantalla durante el recorrido y la app conserva el proceso de captura.", 88, 228, 1080, 70, 24, C.ink);
  label(s, "Qué ocurre internamente", 88, 355, 440);
  textbox(s, "mechanism", "La ubicación se captura durante el viaje.\n\nCada punto se guarda localmente antes de intentar enviarlo.\n\nUna notificación indica que el seguimiento está activo.", 88, 395, 600, 190, 22, C.ink);
  s.shapes.add({ geometry: "rect", name: "condition-background", position: { left: 760, top: 350, width: 395, height: 208 }, fill: C.pale, line: { fill: C.pale, width: 0 } });
  label(s, "Condiciones", 790, 378, 330, C.green);
  textbox(s, "conditions", "Celular encendido\nUbicación activada\nNo cerrar LogiApp a la fuerza", 790, 420, 330, 110, 20, C.ink);
}

// 4 — local persistence
{
  const s = makeSlide(4, "Datos y sincronización", "La operación tolera cortes temporales de internet", "El cierre de viaje y el seguimiento tienen rutas de persistencia local y sincronización posterior. En caso de conectividad intermitente, la interfaz puede informar que el cierre quedó guardado localmente y que las fotos se subirán después. La existencia del mecanismo está comprobada en el código; la tasa de éxito de sincronización no se ha medido. Fuentes: Servicios/BaseDeDatosLocal.cs; Servicios/SincronizadorGlobal.cs; Vistas/PaginaTerminarViaje.xaml.cs.");
  textbox(s, "lead", "La app conserva información pendiente en el dispositivo y la sincroniza cuando vuelve la conexión.", 88, 235, 1050, 72, 27, C.ink, true);
  label(s, "Flujo de datos", 88, 376, 300);
  textbox(s, "flow", "Captura local   →   Conexión disponible   →   Envío a la nube", 88, 420, 1080, 60, 24, C.blue, true);
  textbox(s, "detail", "Este mecanismo reduce la dependencia de una conexión estable durante el trabajo. El resultado operativo debe medirse en una prueba de desconexión y recuperación.", 88, 535, 1060, 72, 20, C.muted);
}

// 5 — data quality and fuel
{
  const s = makeSlide(5, "Calidad del registro", "Los ajustes mejoran la precisión del historial", "Se ajustó el guardado de coordenadas para evitar repeticiones durante la detención sin omitir el punto final de llegada. También se añadió el registro de litros recargados y su uso en el cálculo de consumo. Son cambios de lógica y calidad de datos; no constituyen una medición de velocidad ni de autonomía de batería. Fuentes: Vistas/PaginaTerminarViaje.xaml.cs; Servicios/ConsultasGoogleSheets.cs; commit 3b9ccf0 y e20aac2.");
  label(s, "Coordenadas", 88, 245, 430);
  textbox(s, "gps-copy", "Se filtran puntos repetidos durante la ruta y se conserva la ubicación de llegada.", 88, 289, 480, 126, 24, C.ink);
  label(s, "Combustible", 680, 245, 430);
  textbox(s, "fuel-copy", "La recarga reportada durante el viaje entra en el cálculo de consumo al cerrar.", 680, 289, 480, 126, 24, C.ink);
  s.shapes.add({ geometry: "rect", name: "divider", position: { left: 620, top: 248, width: 2, height: 200 }, fill: C.cyan, line: { fill: C.cyan, width: 0 } });
  textbox(s, "impact", "Efecto esperado: un historial más útil para revisar el recorrido y el consumo.", 88, 520, 1040, 55, 23, C.blue, true);
}

// 6 — measured vs not yet measured
{
  const s = makeSlide(6, "Validación pendiente", "El siguiente paso es medir el comportamiento real", "El repositorio demuestra la implementación de estas capacidades, pero no incluye resultados de pruebas comparativas de rendimiento. Para afirmar mejoras cuantitativas hacen falta una línea base y una prueba repetible. Propuesta: ejecutar un viaje de prueba en dispositivo real, registrar tiempos, desconexión, trayecto y consumo de batería; comparar contra la versión anterior antes de publicar una nueva versión.");
  textbox(s, "statement", "Hay mejoras internas implementadas; su impacto todavía necesita medición.", 88, 225, 1080, 75, 27, C.ink, true);
  label(s, "Indicadores para la siguiente prueba", 88, 352, 600, C.amber);
  const measures = [
    "Puntos GPS recibidos frente a puntos esperados",
    "Sincronizaciones completadas después de perder conexión",
    "Tiempo de apertura y cierre del viaje",
    "Batería y datos consumidos por hora de seguimiento",
  ];
  measures.forEach((m, i) => textbox(s, `measure-${i}`, `${String(i + 1).padStart(2, "0")}   ${m}`, 88, 394 + i * 48, 1050, 38, 19, C.ink));
  textbox(s, "honesty", "No hay resultados comparativos registrados todavía.", 88, 610, 980, 30, 17, C.muted, true);
}

const candidatePath = path.join(TMP_DIR, "candidate.pptx");
await (await PresentationFile.exportPptx(presentation)).save(candidatePath);
for (let i = 0; i < presentation.slides.items.length; i++) {
  const slide = presentation.slides.items[i];
  const png = await presentation.export({ slide, format: "png", scale: 1 });
  await fs.writeFile(path.join(TMP_DIR, `slide-${i + 1}.png`), new Uint8Array(await png.arrayBuffer()));
}
const stagingDir = path.join(TMP_DIR, "finalizer");
await fs.mkdir(stagingDir, { recursive: true });
await fs.mkdir(path.dirname(FINAL_PPTX), { recursive: true });
await finalizePresentation({
  workspaceDir, candidatePath, finalPath: FINAL_PPTX,
  pythonExecutable: "C:/Users/elton/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe",
  integrityValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_package_integrity.py"),
  layoutValidatorPath: path.join(SKILL_DIR, "container_tools/inspect_presentation_layout_geometry.py"),
  layoutArgs: ["--expected-slide-size-emu", "12192000,6858000", "--validate-heading-fit"],
  requiredNativeTableOwnerSlides: [], requiredNativeChartOwnerSlides: [],
  fontPolicy: { basis: "design", families: [fontFamily] }, verifyArtifactToolImport: true,
  receiptPath: path.join(stagingDir, "validation.json"),
});
console.log(JSON.stringify({ finalPath: FINAL_PPTX, fontFamily, slideCount: presentation.slides.items.length }));
