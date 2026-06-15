export interface CapturedRequest<TBody> {
  body: TBody;
  headers: Headers;
  url: URL;
  searchParams: URLSearchParams;
}

export class RequestCapture<TBody> {
  private readonly recorded: CapturedRequest<TBody>[] = [];

  get calls(): ReadonlyArray<CapturedRequest<TBody>> { return this.recorded; }
  get lastRequest(): CapturedRequest<TBody> | undefined { return this.recorded.at(-1); }
  get wasCalled(): boolean { return this.recorded.length > 0; }
  get callCount(): number { return this.recorded.length; }

  record(request: CapturedRequest<TBody>): void {
    this.recorded.push(request);
  }
}
