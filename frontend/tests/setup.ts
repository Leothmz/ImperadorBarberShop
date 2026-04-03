import '@testing-library/jest-dom'
import { beforeAll, afterEach, afterAll } from 'vitest'
import { server } from './mocks/server'

// Start MSW server before all tests
beforeAll(() => server.listen({ onUnhandledRequest: 'warn' }))

// Reset handlers between tests so mutations in one test don't affect others
afterEach(() => server.resetHandlers())

// Clean up after all tests
afterAll(() => server.close())
