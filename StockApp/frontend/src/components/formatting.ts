/**
 * Presentation helpers shared by the table and the export.
 *
 * Prices arrive already reduced to the published precision, so the UI only ever pads them out for
 * display. It never rounds: doing so here would silently override the strategy the user chose.
 */

/** The number of decimal places the API publishes prices at. */
const decimalPlaces = 4;

/** Renders a price with a fixed number of decimals, so columns line up and trailing zeros survive. */
export function formatPrice(value: number): string {
  return value.toFixed(decimalPlaces);
}

/** Renders a share count with thousands separators, as the wireframe shows. */
export function formatVolume(value: number): string {
  return value.toLocaleString('en-US');
}
