import { triggerDownload } from './download'

function escapeCsvCell(value: unknown): string {
  const str = value === null || value === undefined ? '' : String(value)
  return /[",\n]/.test(str) ? `"${str.replace(/"/g, '""')}"` : str
}

/** Builds a CSV from column headers + rows and triggers a browser download. No external dependency. */
export function downloadCsv(filename: string, headers: string[], rows: unknown[][]): void {
  const lines = [headers, ...rows].map((row) => row.map(escapeCsvCell).join(','))
  const blob = new Blob([lines.join('\r\n')], { type: 'text/csv;charset=utf-8;' })
  triggerDownload(filename, blob)
}
