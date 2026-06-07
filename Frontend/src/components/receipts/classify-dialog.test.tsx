import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import type { ReceiptItem } from '@/types/api'

// Mock hooks and sonner before importing component
vi.mock('@/hooks/use-receipt-items', () => ({
  useConfirmClassification: vi.fn(),
  useSaveClassificationRule: vi.fn(() => ({ mutateAsync: vi.fn(), isPending: false })),
}))
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))

// Mock shadcn/ui Dialog — renders children directly so we don't need portals
vi.mock('@/components/ui/dialog', () => ({
  Dialog: ({ children }: { children: React.ReactNode; open?: boolean; onOpenChange?: (v: boolean) => void }) => <div data-testid="dialog">{children}</div>,
  DialogContent: ({ children }: { children: React.ReactNode }) => <div data-testid="dialog-content">{children}</div>,
  DialogHeader: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  DialogTitle: ({ children }: { children: React.ReactNode }) => <h2>{children}</h2>,
  DialogDescription: ({ children }: { children: React.ReactNode }) => <p>{children}</p>,
  DialogFooter: ({ children }: { children: React.ReactNode }) => <div data-testid="dialog-footer">{children}</div>,
}))

// Mock shadcn/ui Select — renders as a native <select> for easy testing
vi.mock('@/components/ui/select', () => ({
  Select: ({ children, value, onValueChange }: {
    children: React.ReactNode
    value?: string
    onValueChange?: (v: string) => void
  }) => (
    <select
      data-testid="category-select"
      value={value ?? ''}
      onChange={(e) => onValueChange?.(e.target.value)}
    >
      {children}
    </select>
  ),
  SelectTrigger: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectValue: ({ placeholder }: { placeholder?: string }) => <option value="">{placeholder}</option>,
  SelectContent: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  SelectItem: ({ children, value }: { children: React.ReactNode; value: string }) => (
    <option value={value}>{children}</option>
  ),
}))

// Mock SaveRuleDialog to avoid deep dependency tree
vi.mock('./save-rule-dialog', () => ({
  SaveRuleDialog: () => <div data-testid="save-rule-dialog" />,
}))

import { ClassifyDialog } from './classify-dialog'
import { useConfirmClassification } from '@/hooks/use-receipt-items'
import { toast } from 'sonner'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

const baseItem: ReceiptItem = {
  id: 'item-1',
  receiptId: 'receipt-1',
  description: 'Taschenrechner TI-30',
  quantity: 1,
  unitPrice: 14.99,
  totalPrice: 14.99,
  lineNumber: 1,
  latestClassification: {
    id: 'cls-1',
    category: 'WerbungskostenArbeitsmittel',
    method: 'AI',
    status: 'Suggested',
    reason: 'Bürozubehör',
    classifiedAt: '2025-01-01T00:00:00Z',
  },
}

describe('ClassifyDialog', () => {
  let mutateAsyncMock: ReturnType<typeof vi.fn>
  let onOpenChangeMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    vi.clearAllMocks()
    mutateAsyncMock = vi.fn().mockResolvedValue({})
    onOpenChangeMock = vi.fn()
    vi.mocked(useConfirmClassification).mockReturnValue({
      mutateAsync: mutateAsyncMock,
      isPending: false,
    } as ReturnType<typeof useConfirmClassification>)
  })

  describe('confirm (manual category override) path', () => {
    it('calls mutateAsync with { itemId, category } and toasts "Klassifizierung bestätigt"', async () => {
      render(
        <ClassifyDialog
          item={baseItem}
          vendor="Amazon"
          open={true}
          onOpenChange={onOpenChangeMock}
        />,
        { wrapper }
      )

      // Select a different category
      const select = screen.getByTestId('category-select')
      await act(async () => {
        fireEvent.change(select, { target: { value: 'WerbungskostenFachliteratur' } })
      })

      // Click the confirm button ("Kategorie zuweisen" or "Vorschlag bestätigen")
      const confirmBtn = screen.getByRole('button', { name: /kategorie zuweisen|vorschlag bestätigen/i })
      await act(async () => {
        fireEvent.click(confirmBtn)
      })

      await vi.waitFor(() => {
        expect(mutateAsyncMock).toHaveBeenCalledWith({
          itemId: 'item-1',
          category: 'WerbungskostenFachliteratur',
        })
        expect(toast.success).toHaveBeenCalledWith('Klassifizierung bestätigt')
      })
    })
  })

  describe('quick-confirm (accept suggestion) path', () => {
    it('calls mutateAsync with the suggested category and toasts "Vorschlag bestätigt"', async () => {
      render(
        <ClassifyDialog
          item={baseItem}
          vendor="Amazon"
          open={true}
          onOpenChange={onOpenChangeMock}
        />,
        { wrapper }
      )

      // The quick-confirm button in the suggestion panel has exact text "Bestätigen"
      // (not "Vorschlag bestätigen" which is the footer button)
      const buttons = screen.getAllByRole('button', { name: /bestätigen/i })
      // The quick-confirm button is the one with exact text "Bestätigen" (no prefix)
      const quickConfirmBtn = buttons.find((b) => b.textContent?.trim() === 'Bestätigen')
      if (!quickConfirmBtn) throw new Error('Quick-confirm button not found')

      await act(async () => {
        fireEvent.click(quickConfirmBtn)
      })

      await vi.waitFor(() => {
        expect(mutateAsyncMock).toHaveBeenCalledWith({
          itemId: 'item-1',
          category: 'WerbungskostenArbeitsmittel',
        })
        expect(toast.success).toHaveBeenCalledWith('Vorschlag bestätigt')
      })
    })
  })
})
