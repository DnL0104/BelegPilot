import { describe, it, expect } from 'vitest'
import { formatCurrency, formatDate, statusLabel, categoryLabel } from './format'

describe('formatCurrency', () => {
  it('formats 1234.5 as de-DE EUR', () => {
    const result = formatCurrency(1234.5)
    expect(result).toMatch(/1\.234,50/)
    expect(result).toMatch(/€/)
  })

  it('formats 0 as de-DE EUR', () => {
    const result = formatCurrency(0)
    expect(result).toMatch(/0,00/)
    expect(result).toMatch(/€/)
  })
})

describe('categoryLabel', () => {
  it('returns German label for WerbungskostenFachliteratur', () => {
    expect(categoryLabel('WerbungskostenFachliteratur')).toBe('Werbungskosten – Fachliteratur')
  })

  it('returns "Nicht zugeordnet" for Unbekannt', () => {
    expect(categoryLabel('Unbekannt')).toBe('Nicht zugeordnet')
  })

  it('returns "Werbungskosten – Arbeitsmittel" for WerbungskostenArbeitsmittel', () => {
    expect(categoryLabel('WerbungskostenArbeitsmittel')).toBe('Werbungskosten – Arbeitsmittel')
  })

  it('returns input unchanged for unknown key', () => {
    expect(categoryLabel('SomethingUnknown')).toBe('SomethingUnknown')
  })
})

describe('statusLabel', () => {
  it('returns "Wird verarbeitet" for Processing', () => {
    expect(statusLabel('Processing')).toBe('Wird verarbeitet')
  })

  it('returns "Hochgeladen" for Uploaded', () => {
    expect(statusLabel('Uploaded')).toBe('Hochgeladen')
  })

  it('returns "Verarbeitet" for Processed', () => {
    expect(statusLabel('Processed')).toBe('Verarbeitet')
  })

  it('returns "Fehlgeschlagen" for Failed', () => {
    expect(statusLabel('Failed')).toBe('Fehlgeschlagen')
  })

  it('returns input unchanged for unknown status', () => {
    expect(statusLabel('UnknownStatus')).toBe('UnknownStatus')
  })
})

describe('formatDate', () => {
  it('formats ISO date string as de-DE date', () => {
    const result = formatDate('2025-01-15T00:00:00Z')
    // de-DE format: DD.MM.YYYY
    expect(result).toMatch(/\d{2}\.\d{2}\.\d{4}/)
  })
})
