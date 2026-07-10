import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import type { ReceiptItem } from '@/types/api'

// CLS-04: per-item AI reasoning must be expand-on-demand (collapsed by default,
// click reveals, click hides) for ALL four classification states — not a title= tooltip.

vi.mock('@/hooks/use-receipt-items', () => ({
  useReceiptItems: vi.fn(),
  useBatchConfirm: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
}))
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }))
// Stub ClassifyDialog to avoid its deep dependency tree (mirrors classify-dialog.test.tsx's save-rule stub).
vi.mock('./classify-dialog', () => ({ ClassifyDialog: () => <div data-testid="classify-dialog" /> }))
// Stub EditItemDialog for the same reason (avoids needing useCorrectReceiptItem in the hooks mock above).
vi.mock('./edit-item-dialog', () => ({ EditItemDialog: () => <div data-testid="edit-item-dialog" /> }))

import { ReceiptItemsTable } from './receipt-items-table'
import { useReceiptItems } from '@/hooks/use-receipt-items'

const REASON = {
  suggested: 'AI: Bueromaterial erkannt',
  unbekannt: 'AI: Konnte nicht zugeordnet werden',
  failed: 'AI-Fehler: Zeitueberschreitung der API',
  confirmed: 'Manuell bestaetigt durch Nutzer',
}

function item(id: string, status: string, category: string, reason: string, tier: 'HIGH' | 'MEDIUM' | 'LOW' | null): ReceiptItem {
  return {
    id,
    receiptId: 'r1',
    description: `Posten ${id}`,
    quantity: 1,
    unitPrice: 9.99,
    totalPrice: 9.99,
    lineNumber: 1,
    latestClassification: {
      id: `cls-${id}`,
      category,
      method: 'AI',
      status,
      confidenceTier: tier,
      reason,
      classifiedAt: '2026-06-22T00:00:00Z',
    },
  }
}

const ITEMS: ReceiptItem[] = [
  item('a', 'Suggested', 'WerbungskostenBueromaterial', REASON.suggested, 'HIGH'),
  item('b', 'Suggested', 'Unbekannt', REASON.unbekannt, 'LOW'),
  item('c', 'Failed', 'Unbekannt', REASON.failed, null),
  item('d', 'Confirmed', 'WerbungskostenBueromaterial', REASON.confirmed, null),
]

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

describe('ReceiptItemsTable — CLS-04 expand-on-demand reasoning', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    ;(useReceiptItems as ReturnType<typeof vi.fn>).mockReturnValue({ data: ITEMS, isLoading: false })
  })

  it('hides every reason by default and renders a real toggle control per item (not a tooltip)', () => {
    const { container } = render(<ReceiptItemsTable receiptId="r1" />, { wrapper })

    // Collapsed by default: no reason text is in the DOM for any of the four states.
    for (const reason of Object.values(REASON)) {
      expect(screen.queryAllByText(reason)).toHaveLength(0)
    }

    // Not tooltip-ONLY: a real expand control (button) exists for items with a reason.
    // (CLS-04 requires the reasoning be expandable on demand, not merely a hover hint.)
    const toggles = screen.getAllByRole('button', { name: /Begründung für .+ einblenden/i })
    expect(toggles.length).toBeGreaterThanOrEqual(4)
    void container
  })

  it('reveals a reason on click and hides it again on a second click (all four states)', () => {
    render(<ReceiptItemsTable receiptId="r1" />, { wrapper })
    const toggles = screen.getAllByRole('button', { name: /Begründung für .+ einblenden/i })

    // Layout order: mobile cards render first (items a–d → toggles 0–3), then the
    // desktop table (items a–d → toggles 4–7). Clicking item i's mobile toggle (index i)
    // expands that item.id in BOTH layouts (state is keyed by item.id).
    const stateReasons = [REASON.suggested, REASON.unbekannt, REASON.failed, REASON.confirmed]
    stateReasons.forEach((reason, i) => {
      const toggle = toggles[i] // item i's mobile toggle
      expect(screen.queryAllByText(reason)).toHaveLength(0) // collapsed
      fireEvent.click(toggle)
      expect(screen.queryAllByText(reason).length).toBeGreaterThan(0) // expanded
      fireEvent.click(toggle)
      expect(screen.queryAllByText(reason)).toHaveLength(0) // collapsed again
    })
  })
})
