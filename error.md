                class="mb-1 font-medium leading-none tracking-tight"
              >
                Couldn't read that automatically
              </h5>
              <div
                class="text-sm [&_p]:leading-relaxed"
              >
                Please switch to the structured form and fill in the details yourself — your text has been kept.
              </div>
            </div>
          </div>
          <p
            class="mt-4 text-sm text-muted-foreground"
          >
            Prefer to type it in yourself? Switch to the structured form above at any time.
          </p>
        </div>
        <div
          aria-labelledby="radix-:r3:-trigger-form"
          class="mt-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          data-orientation="horizontal"
          data-state="inactive"
          hidden=""
          id="radix-:r3:-content-form"
          role="tabpanel"
          tabindex="0"
        />
      </div>
    </div>
  </body>
</html>
 ❯ Proxy.waitForWrapper node_modules/@testing-library/dom/dist/wait-for.js:163:27
 ❯ src/features/disputes/DisputeEntry.test.tsx:37:11
     35|     await user.click(screen.getByRole("button", { name: /extract details/i }));
     36| 
     37|     await waitFor(() => expect(screen.getByLabelText(/merchant/i)).toHaveValue("Shoprite"));
       |           ^
     38|     expect(screen.getByLabelText(/^amount/i)).toHaveValue("450");
     39|     expect(screen.getByLabelText(/category/i)).toHaveValue("DUPLICATE_CHARGE");

⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[1/1]⎯

 Test Files  1 failed | 6 passed (7)
      Tests  1 failed | 18 passed (19)
   Start at  00:09:27
   Duration  3.86s (transform 425ms, setup 3.04s, collect 1.64s, tests 2.10s, environment 6.76s, prepare 963ms)

➜  dispute-portal-ui git:(main) ✗ clear
➜  dispute-portal-ui git:(main) ✗ npm run test

> dispute-portal-ui@0.1.0 test
> vitest run


 RUN  v2.1.1 /Users/BonganiDuma/Desktop/dmc-fin-motion_topicproducer/src/dispute-portal-ui

 ❯ src/features/ops/OpsResolveModal.test.tsx (2)
   ❯ OpsResolveModal (2)
 ✓ src/components/StatusBadge.test.tsx (3)
 ✓ src/features/ops/OpsResolveModal.test.tsx (2) 413ms
 ✓ src/features/ops/api.test.ts (3)
 ❯ src/features/disputes/DisputeEntry.test.tsx (3) 1365ms
   ❯ DisputeEntry (DisputeForm) (3) 1364ms
     ✓ renders both the structured form and natural-language tabs
     × calls the AI extract endpoint and pre-fills fields from the NL description 1177ms
     ✓ keeps submit disabled until required fields are populated
 ✓ src/features/disputes/DisputeList.test.tsx (3)
 ✓ src/features/disputes/DisputeTimeline.test.tsx (3)
 ✓ src/features/disputes/StructuredDisputeForm.test.tsx (2)

⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯ Failed Tests 1 ⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯

 FAIL  src/features/disputes/DisputeEntry.test.tsx > DisputeEntry (DisputeForm) > calls the AI extract endpoint and pre-fills fields from the NL description
TestingLibraryElementError: Unable to find a label with the text of: /merchant/i

Ignored nodes: comments, script, style
<body>
  <div>
    <div
      data-orientation="horizontal"
      dir="ltr"
    >
      <div
        aria-label="Dispute entry method"
        aria-orientation="horizontal"
        class="inline-flex h-10 items-center justify-center rounded-md bg-muted p-1 text-muted-foreground"
        data-orientation="horizontal"
        role="tablist"
        style="outline: none;"
        tabindex="0"
      >
        <button
          aria-controls="radix-:r3:-content-nl"
          aria-selected="true"
          class="inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-background transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm"
          data-orientation="horizontal"
          data-radix-collection-item=""
          data-state="active"
          id="radix-:r3:-trigger-nl"
          role="tab"
          tabindex="0"
          type="button"
        >
          Describe in your own words
        </button>
        <button
          aria-controls="radix-:r3:-content-form"
          aria-selected="false"
          class="inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-background transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm"
          data-orientation="horizontal"
          data-radix-collection-item=""
          data-state="inactive"
          id="radix-:r3:-trigger-form"
          role="tab"
          tabindex="-1"
          type="button"
        >
          Structured form
        </button>
      </div>
      <div
        aria-labelledby="radix-:r3:-trigger-nl"
        class="mt-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        data-orientation="horizontal"
        data-state="active"
        id="radix-:r3:-content-nl"
        role="tabpanel"
        style="animation-duration: 0s;"
        tabindex="0"
      >
        <div
          class="space-y-4"
        >
          <div
            class="space-y-2"
          >
            <label
              class="text-sm font-medium leading-none peer-disabled:opacity-70"
              for="nl-text"
            >
              Describe what happened
            </label>
            <textarea
              class="flex min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 aria-[invalid=true]:ring-2 aria-[invalid=true]:ring-amber-500"
              id="nl-text"
              placeholder="e.g. I was charged R450 twice at Shoprite on 14 July but I only shopped once."
              rows="5"
            >
              I was charged R450 twice at Shoprite on 14 July but I only shopped once.
            </textarea>
          </div>
          <button
            aria-busy="false"
            class="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2"
            type="button"
          >
            Extract details
          </button>
          <div
            class="relative w-full rounded-lg border p-4 [&>svg]:absolute [&>svg]:left-4 [&>svg]:top-4 [&>svg~*]:pl-7 border-destructive/50 text-destructive [&>svg]:text-destructive"
            role="alert"
          >
            <h5
              class="mb-1 font-medium leading-none tracking-tight"
            >
              Couldn't read that automatically
            </h5>
            <div
              class="text-sm [&_p]:leading-relaxed"
            >
              Please switch to the structured form and fill in the details yourself — your text has been kept.
            </div>
          </div>
        </div>
        <p
          class="mt-4 text-sm text-muted-foreground"
        >
          Prefer to type it in yourself? Switch to the structured form above at any time.
        </p>
      </div>
      <div
        aria-labelledby="radix-:r3:-trigger-form"
        class="mt-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        data-orientation="horizontal"
        data-state="inactive"
        hidden=""
        id="radix-:r3:-content-form"
        role="tabpanel"
        tabindex="0"
      />
    </div>
  </div>
</body>

Ignored nodes: comments, script, style
<html>
  <head />
  <body>
    <div>
      <div
        data-orientation="horizontal"
        dir="ltr"
      >
        <div
          aria-label="Dispute entry method"
          aria-orientation="horizontal"
          class="inline-flex h-10 items-center justify-center rounded-md bg-muted p-1 text-muted-foreground"
          data-orientation="horizontal"
          role="tablist"
          style="outline: none;"
          tabindex="0"
        >
          <button
            aria-controls="radix-:r3:-content-nl"
            aria-selected="true"
            class="inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-background transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm"
            data-orientation="horizontal"
            data-radix-collection-item=""
            data-state="active"
            id="radix-:r3:-trigger-nl"
            role="tab"
            tabindex="0"
            type="button"
          >
            Describe in your own words
          </button>
          <button
            aria-controls="radix-:r3:-content-form"
            aria-selected="false"
            class="inline-flex items-center justify-center whitespace-nowrap rounded-sm px-3 py-1.5 text-sm font-medium ring-offset-background transition-all focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 data-[state=active]:bg-background data-[state=active]:text-foreground data-[state=active]:shadow-sm"
            data-orientation="horizontal"
            data-radix-collection-item=""
            data-state="inactive"
            id="radix-:r3:-trigger-form"
            role="tab"
            tabindex="-1"
            type="button"
          >
            Structured form
          </button>
        </div>
        <div
          aria-labelledby="radix-:r3:-trigger-nl"
          class="mt-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          data-orientation="horizontal"
          data-state="active"
          id="radix-:r3:-content-nl"
          role="tabpanel"
          style="animation-duration: 0s;"
          tabindex="0"
        >
          <div
            class="space-y-4"
          >
            <div
              class="space-y-2"
            >
              <label
                class="text-sm font-medium leading-none peer-disabled:opacity-70"
                for="nl-text"
              >
                Describe what happened
              </label>
              <textarea
                class="flex min-h-[80px] w-full rounded-md border border-input bg-background px-3 py-2 text-sm ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 aria-[invalid=true]:ring-2 aria-[invalid=true]:ring-amber-500"
                id="nl-text"
                placeholder="e.g. I was charged R450 twice at Shoprite on 14 July but I only shopped once."
                rows="5"
              >
                I was charged R450 twice at Shoprite on 14 July but I only shopped once.
              </textarea>
            </div>
            <button
              aria-busy="false"
              class="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:pointer-events-none disabled:opacity-50 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2"
              type="button"
            >
              Extract details
            </button>
            <div
              class="relative w-full rounded-lg border p-4 [&>svg]:absolute [&>svg]:left-4 [&>svg]:top-4 [&>svg~*]:pl-7 border-destructive/50 text-destructive [&>svg]:text-destructive"
              role="alert"
            >
              <h5
                class="mb-1 font-medium leading-none tracking-tight"
              >
                Couldn't read that automatically
              </h5>
              <div
                class="text-sm [&_p]:leading-relaxed"
              >
                Please switch to the structured form and fill in the details yourself — your text has been kept.
              </div>
            </div>
          </div>
          <p
            class="mt-4 text-sm text-muted-foreground"
          >
            Prefer to type it in yourself? Switch to the structured form above at any time.
          </p>
        </div>
        <div
          aria-labelledby="radix-:r3:-trigger-form"
          class="mt-4 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          data-orientation="horizontal"
          data-state="inactive"
          hidden=""
          id="radix-:r3:-content-form"
          role="tabpanel"
          tabindex="0"
        />
      </div>
    </div>
  </body>
</html>
 ❯ Proxy.waitForWrapper node_modules/@testing-library/dom/dist/wait-for.js:163:27
 ❯ src/features/disputes/DisputeEntry.test.tsx:37:11
     35|     await user.click(screen.getByRole("button", { name: /extract details/i }));
     36| 
     37|     await waitFor(() => expect(screen.getByLabelText(/merchant/i)).toHaveValue("Shoprite"));
       |           ^
     38|     expect(screen.getByLabelText(/^amount/i)).toHaveValue("450");
     39|     expect(screen.getByLabelText(/category/i)).toHaveValue("DUPLICATE_CHARGE");

⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[1/1]⎯

 Test Files  1 failed | 6 passed (7)
      Tests  1 failed | 18 passed (19)
   Start at  00:09:49
   Duration  3.26s (transform 282ms, setup 2.53s, collect 1.04s, tests 2.18s, environment 5.35s, prepare 646ms)

➜  dispute-portal-ui git:(main) ✗ 