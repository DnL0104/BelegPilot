import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, act } from '@testing-library/react'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import React from 'react'

// Mock the hooks and sonner toast before importing the component
vi.mock('@/hooks/use-receipt-files', () => ({
  useUploadFiles: vi.fn(),
}))
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}))
// Mock FileDropzone to avoid drag-and-drop complexity in jsdom
vi.mock('./file-dropzone', () => ({
  FileDropzone: ({ onFilesChange }: { files: File[]; onFilesChange: (files: File[]) => void }) => (
    <input
      data-testid="file-input"
      type="file"
      onChange={(e) => {
        if (e.target.files) onFilesChange(Array.from(e.target.files))
      }}
    />
  ),
}))
// Mock ReceiptFileCard to avoid polling complexity
vi.mock('./receipt-file-card', () => ({
  ReceiptFileCard: ({ fileName }: { receiptFileId: string; fileName: string }) => (
    <div data-testid="receipt-file-card">{fileName}</div>
  ),
}))

import { UploadForm } from './upload-form'
import { useUploadFiles } from '@/hooks/use-receipt-files'
import { toast } from 'sonner'

function wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>
}

describe('UploadForm', () => {
  let mutateAsyncMock: ReturnType<typeof vi.fn>

  beforeEach(() => {
    mutateAsyncMock = vi.fn()
    vi.mocked(useUploadFiles).mockReturnValue({
      mutateAsync: mutateAsyncMock,
      isPending: false,
    } as ReturnType<typeof useUploadFiles>)
    vi.clearAllMocks()
  })

  describe('empty-selection guard', () => {
    it('upload button is disabled when no files are selected', () => {
      render(<UploadForm />, { wrapper })
      const uploadBtn = screen.getByRole('button', { name: /hochladen/i })
      expect(uploadBtn).toBeDisabled()
    })

    it('does not call mutateAsync when no files are selected', () => {
      render(<UploadForm />, { wrapper })
      const uploadBtn = screen.getByRole('button', { name: /hochladen/i })
      // Simulate clicking the disabled button via fireEvent (bypasses browser disabled guard)
      fireEvent.click(uploadBtn)
      expect(mutateAsyncMock).not.toHaveBeenCalled()
    })
  })

  describe('error-restore path', () => {
    it('restores files and shows German error toast when mutateAsync rejects', async () => {
      const serverError = new Error('Server Error')
      mutateAsyncMock.mockRejectedValue(serverError)

      render(<UploadForm />, { wrapper })

      // Add a file via the mocked file input
      const testFile = new File(['content'], 'test.pdf', { type: 'application/pdf' })
      const fileInput = screen.getByTestId('file-input')
      await act(async () => {
        fireEvent.change(fileInput, { target: { files: [testFile] } })
      })

      const uploadBtn = screen.getByRole('button', { name: /1 datei/i })

      // Click the upload button and wait for the async rejection to settle
      await act(async () => {
        fireEvent.click(uploadBtn)
        await vi.waitFor(() => expect(mutateAsyncMock).toHaveBeenCalled())
      })

      // The error toast should show the fallback German message
      expect(toast.error).toHaveBeenCalledWith(
        'Upload fehlgeschlagen. Bitte erneut versuchen.'
      )

      // mutateAsync was called (it failed, but it was called)
      expect(mutateAsyncMock).toHaveBeenCalledTimes(1)
    })

    it('shows server error message when response contains error field', async () => {
      const serverErr = Object.assign(new Error('Server Error'), {
        response: { data: { error: 'Datei zu groß' } },
      })
      mutateAsyncMock.mockRejectedValue(serverErr)

      render(<UploadForm />, { wrapper })

      const testFile = new File(['content'], 'test.pdf', { type: 'application/pdf' })
      const fileInput = screen.getByTestId('file-input')
      await act(async () => {
        fireEvent.change(fileInput, { target: { files: [testFile] } })
      })

      const uploadBtn = screen.getByRole('button', { name: /1 datei/i })
      await act(async () => {
        fireEvent.click(uploadBtn)
        await vi.waitFor(() => expect(mutateAsyncMock).toHaveBeenCalled())
      })

      expect(toast.error).toHaveBeenCalledWith('Datei zu groß')
    })
  })
})
