# Verificación de preparación de compras y recepciones

Revisión del 14 de noviembre de 2026. Todos los ensayos SQL se realizaron en bases nuevas `Perfumeria_*_Test_*`; se conservaron la configuración de desarrollo y los datos existentes.

| Verificación | Resultado |
|---|---|
| Pruebas unitarias Release | 79 correctas; incluye sesión ausente, inactiva y sin permiso antes de acceder a datos |
| Componentes WinForms y SQL Server 16 | 160 verificaciones correctas |
| Componentes WinForms y LocalDB 17 | Las mismas 160 verificaciones correctas |
| Evaluación de proveedores N03 | 53 verificaciones correctas |
| PDF e importación/exportación XML | 39 verificaciones correctas |
| Precarga y procesos por actor | 62 verificaciones correctas |
| Compilación Release e Inno Setup 6 | Correctas; instalador actualizado |

Las pruebas nuevas comprueban importes ambiguos sin escrituras, precisión y rangos sin recortes, recuperación después de corregir, borrador oculto todavía pendiente, Nuevo/Cancelar con descarte aceptado y rechazado, dos perfumes con total correcto, actualización sin duplicar, eliminación de una línea e inclusión del último producto escrito al crear. El circuito continúa por revisión contable y emisión usando los actores correspondientes.

En recepción se verificaron cambios de orden y perfume con aceptación/rechazo del descarte, cantidad o vencimiento como únicos datos preparados, conservación al actualizar, refresco inmediato de stock y trazabilidad al guardar, y una recepción externa que reduce el saldo: conserva el texto original, identifica el exceso y bloquea un nuevo ingreso. Las pruebas operativas también ejercitan acceso y menús de Compras, Contabilidad, Depósito y Ventas. Se revisó visualmente el formulario nativo y su tabla de borrador.

En Enterprise Architect se actualizaron `N01-06a` (varios renglones y guardado de la lista) y `N01-06b` (consulta automática de stock y trazabilidad dentro de la confirmación). Se mantienen actores, nombres de clases, métodos reales, retornos tipados discontinuos y activaciones continuas. No cambian entidades, tablas ni relaciones de clases del dominio: las vistas de clases, actividades y DER mantienen su alcance.

Google Docs conserva 13 tablas, 48 imágenes y 27 diagramas EA únicos, ubicados en sus secciones y sin recortes. Se reemplazaron dos capturas y siete párrafos de las especificaciones de compra/recepción, con lenguaje abstracto. La comparación contra la exportación previa está en `EnterpriseArchitect/verification-demo-document.json`; el resto del texto e imágenes se conserva. Google reduce las capturas a un máximo de 2048 píxeles; se verificaron proporciones y contenido. Se revisaron las páginas afectadas del PDF exportado por Google Docs, porque el renderizador local requiere LibreOffice y no está instalado.

El EXE del instalador se recompiló con los nuevos binarios; esta revisión no añade migraciones ni cambia el asistente de instalación. Su compilación no sustituye una instalación gráfica en la computadora de la demo.
