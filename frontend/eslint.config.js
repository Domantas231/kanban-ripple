import js from '@eslint/js'
import globals from 'globals'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import tseslint from 'typescript-eslint'
import eslintConfigPrettier from 'eslint-config-prettier'
import boundaries from 'eslint-plugin-boundaries'
import { defineConfig, globalIgnores } from 'eslint/config'

// Phase 7 — Architectural enforcement (bulletproof-react layout).
//
// Element types and dependency direction:
//
//   shared layers  ←  features  ←  routes  ←  app
//
//   - shared MUST NOT import from features, routes, or app
//   - features MUST NOT import from sibling features
//     (only via the target feature's public `index.ts`)
//   - routes import from features and shared freely
//   - app composes everything; nothing imports from app
//
// Two complementary lint rules enforce this:
//   1. `boundaries/element-types` — controls which element types may
//      import which (the dependency direction above).
//   2. `boundaries/entry-point` — when crossing into a *different*
//      feature, only its `index.ts` may be entered.
//
// Intra-feature imports (auth → auth) are treated as same-element by
// the boundaries plugin (matching `featureName` capture), so absolute
// paths inside the same feature remain allowed.

const SHARED_TYPES = [
  'shared-components',
  'shared-hooks',
  'shared-lib',
  'shared-stores',
  'shared-types',
  'shared-utils',
  'shared-config',
  'shared-assets',
]

export default defineConfig([
  globalIgnores(['dist', 'coverage', 'src/app/routeTree.gen.ts']),
  {
    files: ['**/*.{ts,tsx}'],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
      eslintConfigPrettier,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    plugins: {
      boundaries,
    },
    settings: {
      // Resolve `@/...` alias imports so boundaries can map them to
      // their on-disk location and assign element types correctly.
      'import/resolver': {
        typescript: {
          project: './tsconfig.app.json',
        },
      },
      'boundaries/include': ['src/**/*'],
      'boundaries/elements': [
        { type: 'app', pattern: 'src/app/**/*' },
        { type: 'route', pattern: 'src/routes/**/*' },
        {
          type: 'feature',
          pattern: 'src/features/*/**/*',
          capture: ['featureName'],
        },
        { type: 'testing', pattern: 'src/testing/**/*' },
        { type: 'shared-components', pattern: 'src/components/**/*' },
        { type: 'shared-hooks', pattern: 'src/hooks/**/*' },
        { type: 'shared-lib', pattern: 'src/lib/**/*' },
        { type: 'shared-stores', pattern: 'src/stores/**/*' },
        { type: 'shared-types', pattern: 'src/types/**/*' },
        { type: 'shared-utils', pattern: 'src/utils/**/*' },
        { type: 'shared-config', pattern: 'src/config/**/*' },
        { type: 'shared-assets', pattern: 'src/assets/**/*' },
      ],
      'boundaries/ignore': [
        'src/index.css',
        'src/app/routeTree.gen.ts',
        '**/*.test.{ts,tsx}',
        '**/*.unit.test.{ts,tsx}',
      ],
    },
    rules: {
      'boundaries/element-types': [
        'error',
        {
          default: 'disallow',
          rules: [
            {
              from: ['app'],
              allow: ['app', 'route', 'feature', 'testing', ...SHARED_TYPES],
            },
            {
              from: ['route'],
              allow: ['route', 'feature', ...SHARED_TYPES],
            },
            {
              from: ['feature'],
              allow: ['feature', ...SHARED_TYPES],
            },
            {
              from: ['testing'],
              allow: ['testing', 'feature', 'route', ...SHARED_TYPES],
            },
            // Shared layers may only reach other shared layers.
            // This is the rule that prevents shared → features leaks.
            {
              from: SHARED_TYPES,
              allow: SHARED_TYPES,
            },
          ],
        },
      ],
      'boundaries/entry-point': [
        'error',
        {
          default: 'disallow',
          rules: [
            // Allow any entry point for non-feature elements.
            { target: ['app', 'route', 'testing', ...SHARED_TYPES], allow: '*' },
            // Within the same feature, any file may be imported by
            // path. The `{{from.featureName}}` template ties the
            // target capture to the source file's feature name.
            {
              target: [['feature', { featureName: '{{from.featureName}}' }]],
              allow: '*',
            },
            // Crossing into a *different* feature is only allowed via
            // the public `index.ts` entry point.
            { target: ['feature'], allow: 'index.{ts,tsx}' },
          ],
        },
      ],
    },
  },
  // Cheap structural guard outside features. Inside `src/features/<feat>/`
  // intra-feature absolute imports are still useful (and the boundaries
  // plugin already enforces the cross-feature rule), so this rule is
  // scoped to non-feature files where deep `@/features/*/*/*` paths
  // unambiguously cross a feature boundary.
  {
    files: ['src/{app,routes,components,hooks,lib,stores,types,utils,config,testing}/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@/features/*/*/*'],
              message:
                'No deep imports across features — import the feature\'s public `index.ts` (e.g. `@/features/cards`).',
            },
          ],
        },
      ],
    },
  },
])
