# Demo Diagrams

> Titles + diagrams for screen-sharing while filming. 🚀 **Enhance** blocks = production/scale talking points.

---

## 1. Login & Dealership Scoping — global admin vs scoped manager

```mermaid
sequenceDiagram
    actor U as User
    participant FE as React SPA
    participant API as Auth surface (.NET)
    participant TI as GuestTokenIssuer (HS256)
    participant DB as PostgreSQL

    Note over U,DB: Sign-in — 2 zero-setup demo paths + real SSO, all yield the SAME bearer shape
    alt Global admin — "Continue as guest"
        U->>FE: click guest (admin)
        FE->>API: POST /api/auth/guest-login
        API->>TI: mint JWT, NO dealershipId claim
        TI-->>FE: bearer + profile (sees ALL dealerships)
    else Scoped manager — "Continue as scoped manager"
        U->>FE: click scoped
        FE->>API: POST /api/auth/scoped-login
        API->>DB: pick first dealership
        API->>TI: mint JWT WITH dealershipId claim
        TI-->>FE: bearer + profile (scoped to one dealership)
    else Real SSO — Microsoft Entra ID
        U->>FE: Microsoft sign-in
        FE->>API: GET /api/auth/login → OIDC challenge
        Note right of API: Entra-issued JWT validated by the SAME<br/>bearer scheme — downstream code is identical
    end

    Note over FE,DB: Every later request carries Authorization: Bearer …
    U->>FE: open a vehicle
    FE->>API: GET /api/vehicles/{id}
    API->>API: read dealershipId claim → resolve scope
    API->>DB: WHERE Id={id} AND (claim IS NULL OR DealershipId = claim)
    alt In scope, or global admin (no claim)
        DB-->>FE: 200 — vehicle detail
    else Cross-dealership access (IDOR attempt)
        DB-->>FE: 404 Not Found (never 403 — don't leak that the ID exists)
    end
```

🚀 **Enhance: Production-Ready Auth Architecture**
Hybrid model: **"Identity from SSO – Authorization (RBAC) from Local Database"**
*   **Authentication (SSO)**: Authenticate via Entra ID/Okta + JIT (Just-In-Time) user provisioning to local DB on first sign-in.
*   **Authorization (RBAC)**: Manage granular roles and relationships (`Users`, `Roles`, `UserDealerships` many-to-many) in local DB $\rightarrow$ Enrich JWT using ASP.NET Core `IClaimsTransformation`.
*   **Benefits**: Prevents JWT Token Bloat, supports fine-grained policy-based auth, and enables UI Dealership Selector context switching.

---

## 2. Capital Exposure — where every number comes from

```mermaid
flowchart TB
    subgraph DB["PostgreSQL — stored facts (single source of truth)"]
        F1["Vehicle row<br/>AcquisitionDate · AcquisitionCost · ListPrice · Status · ClosedDate"]
    end
    subgraph CFG["Config — appsettings (no redeploy to tune)"]
        C1["AgingConfig<br/>Fresh ≤30 · Watch ≤60 · Aging ≤90 · Critical 91+ days"]
        C2["CarryingCostConfig<br/>APR 9% · depreciation 0.04%/day · $4/day fixed holding"]
    end
    CLK["IClock (injected)<br/>real clock in prod · fixed clock in tests → deterministic"]

    F1 --> AGE["AgingCalculator (pure)<br/>daysInInventory = asOf − AcquisitionDate<br/>→ tier + daysUntilAging"]
    F1 --> CC["CarryingCostCalculator (pure)<br/>daily = cost×(APR/365) + cost×depr + $4<br/>toDate = daysInInventory × daily"]
    C1 --> AGE
    C2 --> CC
    CLK --> AGE
    CLK --> CC

    AGE --> PER["Per-vehicle derived fields<br/><b>never stored — computed on every read</b>"]
    CC --> PER
    PER --> AGG["InventoryService.GetSummaryAsync<br/>aggregate over ACTIVE fleet (InStock + Reserved)"]
    AGG --> KPI["InventorySummaryDto → dashboard block 01<br/>Total units · Inventory value · Avg days<br/>Aged % · <b>Capital tied in aged</b> · Total carrying cost"]

    classDef store fill:#eef2fb,stroke:#5570aa,color:#1a2a4a;
    classDef cfg fill:#fdf3e7,stroke:#cc9933,color:#5a3d0a;
    classDef clock fill:#eaf6ec,stroke:#4a9a5a,color:#123a1a;
    class DB,F1 store;
    class CFG,C1,C2 cfg;
    class CLK clock;
```

---

## 3. Inventory List — search, filter, sort, pagination

```mermaid
flowchart TB
    Q["GET /api/vehicles<br/>?search &amp; make &amp; model &amp; status &amp; tier &amp; minDays &amp; maxDays &amp; sort &amp; page &amp; pageSize"]
    Q --> SCOPE["Apply token scope first<br/>dealershipId claim wins over any query param"]
    SCOPE --> S1

    subgraph STAGE1["Stage 1 — in PostgreSQL (SQL over STORED, indexable columns)"]
        S1["WHERE filters:<br/>search → substring over make · model · trim · vin · color · year<br/>+ make · model · status · active/closed scope"]
    end
    S1 --> S2

    subgraph STAGE2["Stage 2 — in memory (DERIVED fields, from the pure calculators)"]
        S2["Compute tier + carrying cost per row<br/>then filter: tier · minDays · maxDays"]
        S3["Sort — default −daysInInventory (most-aged first)<br/>or listPrice · make · year · carryingCost, ± direction"]
        S4["Paginate — Skip/Take · pageSize clamped to ≤ 100"]
        S2 --> S3 --> S4
    end
    S4 --> R["PagedResult&lt;VehicleListItemDto&gt;<br/>items + page + pageSize + totalCount"]

    classDef db fill:#eef2fb,stroke:#5570aa,color:#1a2a4a;
    classDef mem fill:#f2eefb,stroke:#7755aa,color:#2a1a4a;
    class STAGE1,S1 db;
    class STAGE2,S2,S3,S4 mem;
```

🚀 **Enhance: Scaling to Millions of Records (Trade-offs)**
*   **Current Bottleneck**: In-memory derived-field filtering forces **in-memory sorting and pagination (Skip/Take)** $\rightarrow$ Memory/CPU spike if Stage 1 yields millions of rows.
    *   *Mitigation in place*: Dealership scoping (via JWT claims) and active status filters restrict database output to ~100-500 active cars per request, making in-memory calculation safe and viable.
*   **Solution 1: Persist via Scheduler (Daily Batch Job)**
    *   *How*: Compute and persist `AgingTier` and `CarryingCost` in DB tables nightly (e.g., Hangfire/Quartz).
    *   *Trade-off*: Enables index-backed SQL filtering/pagination (extremely fast) vs. Loss of real-time currency accuracy (data updated daily).
*   **Solution 2: Search Engine Integration (Elasticsearch)**
    *   *How*: Stream vehicle edits to a search index; perform calculations during ingestion or indexing.
    *   *Trade-off*: Sub-millisecond multi-dimensional filtering and search at scale vs. Increased architecture complexity and eventual consistency.

---

## 4a. Action Lifecycle — decision of record

```mermaid
stateDiagram-v2
    direction LR
    [*] --> Proposed: create action (type + note) — always starts here
    Proposed --> Approved
    Approved --> InProgress
    InProgress --> Resolved: outcome REQUIRED (Sold / NotSold)
    Resolved --> [*]

    note right of InProgress
        Side-effect — price change happens HERE:
        if type = PriceReduction and proposedValue > 0
        → Vehicle.ListPrice = proposedValue
        (Proposed/Approved are just planning states)
    end note
    note right of Resolved
        Side-effect — the deal lands:
        if outcome = Sold → Vehicle.Close(destination, today)
        freezes aging + carrying cost, exits the risk ledger
    end note
```

## 4b. Vehicle Status — reserve / release / close

```mermaid
stateDiagram-v2
    direction LR
    [*] --> InStock
    InStock --> Reserved: POST /reserve
    Reserved --> InStock: POST /release
    InStock --> Closed: action resolves Sold
    Reserved --> Closed: action resolves Sold
    Closed --> [*]
    note right of Closed
        Sold / Transferred / AtAuction
        ClosedDate stamped → metrics frozen
        history read-only · further writes → 409
    end note
```

🚀 **Enhance: Action Lifecycle & Side-effects**
*   **Role-based Transitions**: Restrict state changes by user roles (e.g., Sales Advisor can only `Propose`, General Manager must `Approve`).
*   **Event-Driven Side-effects**: Publish Domain Events (`VehiclePriceChanged`, `VehicleClosed`) to async messaging queues (RabbitMQ/Kafka) to sync external listings (Autotrader) and update financial accounting systems.

---

## 5. AI Recommendation — grounded, baseline-first, fails safe

```mermaid
flowchart TB
    REQ["GET /api/vehicles/{id}/recommendation<br/>(rate-limited — cost/abuse guard)"] --> CACHE{"cached for<br/>this vehicle?"}
    CACHE -->|hit| OUT
    CACHE -->|miss| CLOSED{"vehicle<br/>closed?"}
    CLOSED -->|yes| C409["409 — history only,<br/>zero LLM spend"]
    CLOSED -->|no| CTX["Build RecommendationContext<br/>days · tier · margin · carrying cost · fleet benchmarks"]
    CTX --> BASE["BaselineRecommender — pure, deterministic, unit-tested<br/>Fresh→monitor · Watch→promote<br/>Aging→−5% · Critical→−10%, or auction if margin ≤ 5%<br/><b>ALWAYS produces a result</b>"]
    BASE --> ENABLED{"LLM enabled<br/>&amp; configured?"}
    ENABLED -->|no / disabled| USEBASE
    ENABLED -->|yes| CALL["HttpAiClient.EnrichAsync<br/>Polly v8: timeout · retry · circuit-breaker"]
    CALL -->|throws / HTTP error| USEBASE
    CALL -->|response| VALID{"AiRecommendationValidator<br/>known action? rationale present?<br/>price sane: positive, ≤ list, cut ≤ 50%?"}
    VALID -->|invalid| USEBASE["<b>Source = Baseline</b><br/>deterministic result served"]
    VALID -->|valid| USEAI["<b>Source = AI-enriched</b><br/>LLM rewords rationale + adds market read<br/>numbers still come from the baseline"]
    USEBASE --> CSET
    USEAI --> CSET["cache per vehicle"]
    CSET --> OUT["RecommendationDto → panel + Source badge"]

    classDef safe fill:#eaf6ec,stroke:#4a9a5a,color:#123a1a;
    classDef ai fill:#f2eefb,stroke:#7755aa,color:#2a1a4a;
    classDef stop fill:#fdeaea,stroke:#c0504d,color:#4a1212;
    class BASE,USEBASE safe;
    class USEAI,CALL ai;
    class C409 stop;
```

🚀 **Enhance: Production-Ready AI Recommendation**
*   **Real-time Market Grounding (RAG)**: Connect with automotive market APIs (AutoTrader/Edmunds) to feed real-time competitor prices, local days-supply, and auction trends into the LLM context.
*   **Feedback Loop & LLM Observability**: Track user interaction with recommendations (Accept / Ignore / Edit) and monitor prompts via LLM observability platforms (LangSmith/Arize) to run A/B testing on pricing models.

---

## 6. AI Collaboration & Verification Loop — how I used GenAI as a co-pilot

```mermaid
flowchart TD
    A["1. Discovery & Scenario Selection<br/>AI analyzes 4 scenarios against personal techstack/domain<br/>→ Selected Scenario B (Supply Domain)"]
    B["2. Modular Planning<br/>Discuss techstack & feature framework with AI<br/>→ Created 4-Phase blueprint & test criteria"]
    
    subgraph LOOP["3. Iterative Build & Verification Loop (Phase 1 to 4)"]
        C["Isolated Context (Build Bot)<br/>New Chat per Phase to write clean code & tests<br/>(No context drift or token bloat)"]
        D["Adversarial Audit (Validator Bot)<br/>Separate Chat with independent AI to run builds,<br/>execute tests, and verify endpoints against criteria"]
        E["Human-in-the-Loop<br/>User manually reviews code & tests<br/>to verify design intent"]
        F{"Verification Gate"}
        
        C --> D --> E --> F
        F -->|FAIL: Issues found| C
    end
    
    B --> C
    F -->|PASS: Advanced| G["4. UI Mockups (Claude-Design)<br/>Generated 3 distinct visual mockup styles<br/>to replace generic/basic default UI"]
    G --> H["5. UI Skinning & Polish<br/>Implements selected premium theme<br/>+ Personal refactoring pass on server code"]
    H --> I["6. Final Re-verification<br/>Run final test suite, polish docs (System-Design/README/Script)<br/>→ Clean Production Build & Submission"]

    classDef phase fill:#eef2fb,stroke:#5570aa,color:#1a2a4a;
    classDef loop fill:#f2eefb,stroke:#7755aa,color:#2a1a4a;
    classDef final fill:#eaf6ec,stroke:#4a9a5a,color:#123a1a;
    class A,B phase;
    class C,D,E loop;
    class G,H,I final;
```
