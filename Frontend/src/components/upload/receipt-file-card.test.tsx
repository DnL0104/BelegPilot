import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'

vi.mock('@/hooks/use-receipt-files', () => ({
  useReceiptFileStatus: vi.fn(),
  useCancelReceiptFile: vi.fn(),
  useRetryReceiptFile: vi.fn(),
}))

import { ReceiptFileCard } from './receipt-file-card'
import {
  useReceiptFileStatus,
  useCancelReceiptFile,
  useRetryReceiptFile,
} from '@/hooks/use-receipt-files'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

describe('ReceiptFileCard', () => {
  const receiptFileId = 'file-11111111-1111-1111-1111-111111111111'
  const receiptId = 'receipt-22222222-2222-2222-2222-222222222222'

  beforeEach(() => {
    vi.mocked(useCancelReceiptFile).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as ReturnType<typeof useCancelReceiptFile>)
    vi.mocked(useRetryReceiptFile).mockReturnValue({
      mutate: vi.fn(),
      isPending: false,
    } as ReturnType<typeof useRetryReceiptFile>)
  })

  it('links to /receipts/{receiptId} (not the receiptFileId) once completed', () => {
    // Regression test: the "Beleg ansehen" link previously used receiptFileId as the
    // route param, but the receipt detail page fetches by the Receipt's own id — a
    // different GUID — which 404'd and produced a blank/broken detail page.
    vi.mocked(useReceiptFileStatus).mockReturnValue({
      data: { status: 'Completed', updatedAt: new Date().toISOString(), receiptId },
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useReceiptFileStatus>)

    render(<ReceiptFileCard receiptFileId={receiptFileId} fileName="test.pdf" />, { wrapper })

    const link = screen.getByRole('link', { name: /beleg ansehen/i })
    expect(link).toHaveAttribute('href', `/receipts/${receiptId}`)
  })

  it('shows a retry button for a non-terminal status and triggers the mutation on click', () => {
    const mutate = vi.fn()
    vi.mocked(useReceiptFileStatus).mockReturnValue({
      data: { status: 'Pending', updatedAt: new Date().toISOString() },
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useReceiptFileStatus>)
    vi.mocked(useRetryReceiptFile).mockReturnValue({
      mutate,
      isPending: false,
    } as ReturnType<typeof useRetryReceiptFile>)

    render(<ReceiptFileCard receiptFileId={receiptFileId} fileName="test.pdf" />, { wrapper })

    const retryButton = screen.getByRole('button', { name: /erneut versuchen/i })
    fireEvent.click(retryButton)

    expect(mutate).toHaveBeenCalledWith(receiptFileId)
  })

  it('does not render the link when receiptId is not yet available', () => {
    vi.mocked(useReceiptFileStatus).mockReturnValue({
      data: { status: 'Completed', updatedAt: new Date().toISOString() },
      isLoading: false,
      isError: false,
    } as ReturnType<typeof useReceiptFileStatus>)

    render(<ReceiptFileCard receiptFileId={receiptFileId} fileName="test.pdf" />, { wrapper })

    expect(screen.queryByRole('link', { name: /beleg ansehen/i })).not.toBeInTheDocument()
  })
})
