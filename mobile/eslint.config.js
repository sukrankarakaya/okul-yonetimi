// docs/architecture.md, Forbidden dependencies 7: the app reaches the backend only through
// @okul/api-client — no direct fetch/XMLHttpRequest in app code.
const { defineConfig } = require('eslint/config');
const expoConfig = require('eslint-config-expo/flat');

module.exports = defineConfig([
  expoConfig,
  { ignores: ['node_modules/**', '.expo/**', 'dist/**', 'web-build/**'] },
  {
    files: ['**/*.{ts,tsx}'],
    ignores: ['**/*.test.{ts,tsx}'],
    rules: {
      'no-restricted-globals': [
        'error',
        { name: 'fetch', message: 'Use @okul/api-client (docs/architecture.md, rule 7).' },
        { name: 'XMLHttpRequest', message: 'Use @okul/api-client (docs/architecture.md, rule 7).' },
      ],
      'no-restricted-properties': [
        'error',
        ...['globalThis', 'window', 'self', 'global'].flatMap((object) =>
          ['fetch', 'XMLHttpRequest'].map((property) => ({
            object,
            property,
            message: 'Use @okul/api-client (docs/architecture.md, rule 7).',
          })),
        ),
      ],
      'no-restricted-imports': [
        'error',
        {
          paths: ['axios', 'ky', 'openapi-fetch', 'cross-fetch', 'node-fetch', 'whatwg-fetch', 'superagent'].map((name) => ({
            name,
            message: 'Use @okul/api-client (docs/architecture.md, rule 7).',
          })),
        },
      ],
    },
  },
]);
