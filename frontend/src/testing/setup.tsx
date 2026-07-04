import { afterAll, afterEach, beforeAll } from "vitest";
import "@testing-library/jest-dom/vitest";
import { TestingApiServer } from "@/testing/testing-api-server";

beforeAll(() => TestingApiServer.start());
afterEach(() => TestingApiServer.reset());
afterAll(() => TestingApiServer.stop());
