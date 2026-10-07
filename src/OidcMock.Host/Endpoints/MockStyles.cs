using System.Text.Encodings.Web;

namespace OidcMock.Host.Endpoints;

/// <summary>
/// Hoja de estilos compartida por las pantallas HTML del mock: los tokens Material You dark del
/// prototipo de docs/UI-Prototype (color, forma, tipografia, elevacion y movimiento) y los
/// componentes de pantalla (tarjeta, cabecera, campos y botones). El branding del cliente manda
/// sobre el color primario del sistema; sin branding, la pantalla usa la paleta del prototipo.
/// </summary>
public static class MockStyles
{
    private const string DefaultPrimary = "#4DD0E1";
    private const string DefaultOnPrimary = "#00363A";

    /// <summary>
    /// Los brandings de config son oscuros, asi que el texto del acento va en claro para conservar
    /// el contraste del boton primario.
    /// </summary>
    private const string OnBrandedPrimary = "#FFFFFF";

    public static string Render(string? primaryColor) =>
        $$"""
          <style>
            :root {
              color-scheme: dark;
              --md-primary: {{Escape(primaryColor ?? DefaultPrimary)}};
              --md-on-primary: {{Escape(primaryColor is null ? DefaultOnPrimary : OnBrandedPrimary)}};
              --md-primary-container: #004D54;
              --md-on-primary-container: #6FF7FF;
              --md-secondary: #80CBC4;
              --md-on-secondary: #003731;
              --md-secondary-container: #005048;
              --md-on-secondary-container: #A0F0E7;
              --md-error: #FFB4AB;
              --md-on-error: #690005;
              --md-success: #81C784;
              --md-surface: #111318;
              --md-surface-dim: #111318;
              --md-surface-bright: #37393E;
              --md-on-surface: #E2E2E9;
              --md-on-surface-variant: #C3C6CF;
              --md-outline: #8D9199;
              --md-outline-variant: #43474E;
              --md-surface-container-lowest: #0B0D11;
              --md-surface-container-low: #191C20;
              --md-surface-container: #1D2024;
              --md-surface-container-high: #282A2E;
              --md-surface-container-highest: #333539;
              --md-elevation-1: 0 1px 2px rgba(0,0,0,.3), 0 1px 3px 1px rgba(0,0,0,.15);
              --md-elevation-2: 0 1px 2px rgba(0,0,0,.3), 0 2px 6px 2px rgba(0,0,0,.15);
              --md-elevation-3: 0 4px 8px 3px rgba(0,0,0,.15), 0 1px 3px rgba(0,0,0,.3);
              --md-motion-standard: cubic-bezier(0.2, 0, 0, 1);
              --md-motion-emphasized: cubic-bezier(0.2, 0, 0, 1);
              --md-motion-duration-short: 200ms;
              --md-motion-duration-medium: 400ms;
              --md-shape-xs: 4px;
              --md-shape-sm: 8px;
              --md-shape-md: 12px;
              --md-shape-lg: 16px;
              --md-shape-xl: 28px;
              --md-shape-full: 9999px;
              --md-type-display: 700 3.5rem/1.15 'Inter', system-ui, sans-serif;
              --md-type-headline: 600 2rem/1.25 'Inter', system-ui, sans-serif;
              --md-type-title: 600 1.125rem/1.4 'Inter', system-ui, sans-serif;
              --md-type-body: 400 0.9375rem/1.6 'Inter', system-ui, sans-serif;
              --md-type-label: 500 0.8125rem/1.4 'Inter', system-ui, sans-serif;
              --md-type-label-sm: 500 0.6875rem/1.4 'Inter', system-ui, sans-serif;
            }
            *, *::before, *::after { box-sizing: border-box; }
            body {
              margin: 0;
              padding: 3rem 1rem;
              min-height: 100vh;
              background: var(--md-surface);
              color: var(--md-on-surface);
              font: var(--md-type-body);
              -webkit-font-smoothing: antialiased;
            }
            .card {
              max-width: 26rem;
              margin: 0 auto;
              padding: 2rem;
              background: var(--md-surface-container);
              border: 1px solid var(--md-outline-variant);
              border-radius: var(--md-shape-xl);
              box-shadow: var(--md-elevation-2);
            }
            .card.centered { text-align: center; }
            header {
              display: flex;
              flex-wrap: wrap;
              align-items: center;
              gap: .75rem;
              margin: -2rem -2rem 1.5rem;
              padding: 1.25rem 2rem;
              background: var(--md-surface-container-high);
              border-bottom: 1px solid var(--md-outline-variant);
              border-radius: var(--md-shape-xl) var(--md-shape-xl) 0 0;
            }
            header .identity { display: flex; align-items: center; gap: .75rem; }
            header img { height: 2rem; }
            .mock {
              font: var(--md-type-label-sm);
              text-transform: uppercase;
              letter-spacing: .08em;
              color: var(--md-on-surface-variant);
            }
            h1 { margin: 0; font: var(--md-type-title); color: var(--md-primary); }
            p { margin: 0; color: var(--md-on-surface-variant); }
            label { display: block; margin: 1rem 0 .25rem; font: var(--md-type-label); color: var(--md-on-surface-variant); }
            input {
              width: 100%;
              padding: .65rem .75rem;
              border: 1px solid var(--md-outline-variant);
              border-radius: var(--md-shape-sm);
              background: var(--md-surface-container-highest);
              color: var(--md-on-surface);
              font: var(--md-type-body);
            }
            input:focus-visible, button:focus-visible, a:focus-visible {
              outline: 2px solid var(--md-primary);
              outline-offset: 2px;
            }
            .perfiles { margin: 1rem 0 0; font: var(--md-type-label); color: var(--md-on-surface-variant); }
            .perfiles a { color: var(--md-primary); text-decoration: none; }
            .perfiles a:hover { text-decoration: underline; }
            .scopes {
              margin: 1.25rem 0 0;
              padding: .75rem;
              background: var(--md-surface-container-high);
              border-radius: var(--md-shape-md);
              font: var(--md-type-label);
              color: var(--md-on-surface-variant);
            }
            ul { margin: 1rem 0 0; padding-left: 1.1rem; color: var(--md-on-surface-variant); }
            .actions { display: flex; gap: .75rem; margin-top: 1.5rem; }
            button {
              flex: 1;
              padding: .7rem 1.25rem;
              border: 1px solid var(--md-outline);
              border-radius: var(--md-shape-full);
              background: transparent;
              color: var(--md-on-surface);
              font: var(--md-type-label);
              letter-spacing: .02em;
              cursor: pointer;
              transition: filter var(--md-motion-duration-short) var(--md-motion-standard),
                          background var(--md-motion-duration-short) var(--md-motion-standard);
            }
            .accept { border: 0; background: var(--md-primary); color: var(--md-on-primary); }
            .accept:hover { filter: brightness(1.08); }
            .deny { color: var(--md-primary); }
            .deny:hover { background: rgba(227,226,233,.08); }
            .error { margin: 1rem 0 0; font: var(--md-type-label); color: var(--md-error); }
            iframe { display: none; }
          </style>
          """;

    private static string Escape(string value) => HtmlEncoder.Default.Encode(value);
}