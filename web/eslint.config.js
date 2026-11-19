import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';

export default tseslint.config(
  { ignores: ['dist/**', 'node_modules/**'] },
  js.configs.recommended,
  ...tseslint.configs.recommended,
  reactHooks.configs.flat.recommended,
  {
    files: ['**/*.{ts,tsx}'],
    languageOptions: { globals: globals.browser },
  },
  {
    // docs/architecture.md, Forbidden dependencies 7: the backend only through @okul/api-client.
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/**/*.test.{ts,tsx}', 'src/test/**'],
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
);
