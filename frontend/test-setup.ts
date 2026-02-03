import '@testing-library/jest-dom'
import { vi } from 'vitest'

// Mock Next.js router
vi.mock('next/navigation', () => ({
  useRouter() {
    return {
      push: vi.fn(),
      replace: vi.fn(),
      prefetch: vi.fn(),
      back: vi.fn(),
      forward: vi.fn(),
      refresh: vi.fn(),
    }
  },
  useSearchParams() {
    return new URLSearchParams()
  },
  usePathname() {
    return ''
  },
}))

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
    warning: vi.fn(),
    info: vi.fn(),
  },
}))

// Mock Heroicons
vi.mock('@heroicons/react/24/outline', () => ({
  CheckCircleIcon: vi.fn(() => null),
  XMarkIcon: vi.fn(() => null),
  ExclamationTriangleIcon: vi.fn(() => null),
  InformationCircleIcon: vi.fn(() => null),
  ArrowLeftIcon: vi.fn(() => null),
  DocumentCheckIcon: vi.fn(() => null),
  ChevronDownIcon: vi.fn(() => null),
  ChevronUpIcon: vi.fn(() => null),
  ArrowPathIcon: vi.fn(() => null),
  EllipsisHorizontalIcon: vi.fn(() => null),
}))

// Mock UI components  
vi.mock('@/components/ui/button', () => ({
  Button: vi.fn(({ children }) => children),
}))

vi.mock('@/components/ui/card', () => ({
  Card: vi.fn(({ children }) => children),
  CardContent: vi.fn(({ children }) => children),
  CardHeader: vi.fn(({ children }) => children),
  CardTitle: vi.fn(({ children }) => children),
}))

vi.mock('@/components/ui/badge', () => ({
  Badge: vi.fn(({ children }) => children),
}))

vi.mock('@/components/ui/confidence-indicator', () => ({
  ConfidenceIndicator: vi.fn(() => 'Confidence Indicator'),
}))

vi.mock('@/lib/utils', () => ({
  formatCurrency: (amount: number) => `$${amount.toFixed(2)}`,
  formatDate: (date: string) => new Date(date).toLocaleDateString(),
}))

// Global test setup
global.ResizeObserver = vi.fn().mockImplementation(() => ({
  observe: vi.fn(),
  unobserve: vi.fn(),
  disconnect: vi.fn(),
}))

// Mock window.matchMedia
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation(query => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(), // deprecated
    removeListener: vi.fn(), // deprecated
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
})