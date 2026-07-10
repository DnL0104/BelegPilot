import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'
import type { ReceiptItem } from '@/types/api'

// Mock hooks and sonner before importing component
vi.mock('@/hooks/use-receipt-items', () => ({
  useCorrectReceiptItem: vi.fn(),
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

import { EditItemDialog } from './edit-item-dialog'
import { useCorrectReceiptItem } from '@/hooks/use-receipt-items'
import { toast } from 'sonner'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

const baseItem: ReceiptItem = {
  id: 'item-1',
  receiptId: 'receipt-1',
  description: 'Taschenrechner TI-30',
  quantity: 2,
  unitPrice: 14.99,
  totalPrice: 29.98,
  lineNumber: 1,
  latestClassification: null,
}

describe('EditItemDialog', () => {
  let mutateAsyncMock: ReturnType<typeof vi.fn>
  let onOpenChangeMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    vi.clearAllMocks()
    mutateAsyncMock = vi.fn().mockResolvedValue({})
    onOpenChangeMock = vi.fn()
    vi.mocked(useCorrectReceiptItem).mockReturnValue({
      mutateAsync: mutateAsyncMock,
      isPending: false,
    } as ReturnType<typeof useCorrectReceiptItem>)
  })

  it('renders pre-filled with the item\'s current description/unitPrice/totalPrice', () => {
    render(
      <EditItemDialog item={baseItem} open={true} onOpenChange={onOpenChangeMock} />,
      { wrapper }
    )

    expect(screen.getByLabelText('Beschreibung')).toHaveValue('Taschenrechner TI-30')
    expect(screen.getByLabelText('Einzelpreis (€)')).toHaveValue(14.99)
    expect(screen.getByLabelText('Gesamtpreis (€)')).toHaveValue(29.98)
    expect(screen.getByText(/Menge: 2 Stk\./)).toBeInTheDocument()
  })

  it('submits corrected values via useCorrectReceiptItem and toasts success', async () => {
    render(
      <EditItemDialog item={baseItem} open={true} onOpenChange={onOpenChangeMock} />,
      { wrapper }
    )

    const descriptionInput = screen.getByLabelText('Beschreibung')
    await act(async () => {
      fireEvent.change(descriptionInput, { target: { value: 'Korrigierte Beschreibung' } })
    })

    const saveButton = screen.getByRole('button', { name: 'Speichern' })
    await act(async () => {
      fireEvent.click(saveButton)
    })

    await vi.waitFor(() => {
      expect(mutateAsyncMock).toHaveBeenCalledWith({
        itemId: 'item-1',
        receiptId: 'receipt-1',
        description: 'Korrigierte Beschreibung',
        unitPrice: 14.99,
        totalPrice: 29.98,
      })
      expect(toast.success).toHaveBeenCalledWith('Artikel korrigiert')
      expect(onOpenChangeMock).toHaveBeenCalledWith(false)
    })
  })

  it('shows the empty-description validation error and does not submit', async () => {
    render(
      <EditItemDialog item={baseItem} open={true} onOpenChange={onOpenChangeMock} />,
      { wrapper }
    )

    const descriptionInput = screen.getByLabelText('Beschreibung')
    await act(async () => {
      fireEvent.change(descriptionInput, { target: { value: '   ' } })
    })

    const saveButton = screen.getByRole('button', { name: 'Speichern' })
    await act(async () => {
      fireEvent.click(saveButton)
    })

    await vi.waitFor(() => {
      expect(screen.getByText('Beschreibung darf nicht leer sein.')).toBeInTheDocument()
    })
    expect(mutateAsyncMock).not.toHaveBeenCalled()
  })
})
