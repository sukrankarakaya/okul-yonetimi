/** Component tests (docs/testing.md). Run once, never in watch mode (resource hygiene). */
module.exports = {
  preset: 'jest-expo',
  testMatch: ['<rootDir>/src/**/*.test.{ts,tsx}'],
  watchman: false,
  // Cold start (empty transform cache, first React Native render) took > 5 s under memory pressure;
  // a timeout for loading, not a retry — races are fixed, never waited out (plan 0001, fix round 2).
  testTimeout: 15000,
};
