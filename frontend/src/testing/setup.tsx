import { afterAll, afterEach, beforeAll } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { TestingApiServer } from '@/testing/TestingApiServer';

beforeAll(() => TestingApiServer.start());
afterEach(() => TestingApiServer.reset());
afterAll(() => TestingApiServer.stop());
