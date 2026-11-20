import '@testing-library/jest-dom/vitest'; // types (toBeInTheDocument, …)
import * as matchers from '@testing-library/jest-dom/matchers';
import { cleanup } from '@testing-library/react';
import { afterEach, expect } from 'vitest';

// Bind the matchers to THIS run's expect explicitly — the side-effect import alone left them unbound in some file
// orders (spec 0003 fix round 1: 24 tests failed with "Invalid Chai property: toBeInTheDocument").
expect.extend(matchers);

// Determinism (docs/testing.md): no motion in component tests.
window.matchMedia ??= ((query: string) => ({
  matches: query.includes('prefers-reduced-motion'),
  media: query,
  onchange: null,
  addListener: () => undefined,
  removeListener: () => undefined,
  addEventListener: () => undefined,
  removeEventListener: () => undefined,
  dispatchEvent: () => false,
})) as typeof window.matchMedia;

afterEach(() => cleanup());
