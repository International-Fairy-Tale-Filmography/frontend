# Frontend Launch Polish Plan (Blazor WASM)

## Goal
Bring the modern Blazor WASM frontend to launch quality while preserving the legacy site’s visual layout and search behavior, and improving navigation with modern client-side routing patterns.

## Non-Negotiables
1. Legacy theme and familiar layout stay intact so existing users need no retraining.
2. Search journeys (film, director, country, company, person, origin, language, year) must match legacy outcomes.
3. Modern navigation should be SPA-first (deep-linkable routes, back/forward support, keyboard-friendly controls).
4. GitHub Pages static hosting + CSV-backed data pipeline remains unchanged.

## Current State Snapshot
- **Working**: base shell, home page, banner/theme mostly present, data bootstrap, film details page scaffold, static content pages.
- **Missing/incomplete**:
  - Search is title-only and not parity with legacy behavior.
  - Advanced search UI exists only as a placeholder button.
  - Search result rows lack legacy metadata composition (director/country/company link chains).
  - Film detail links contain hard-coded legacy IDs and broken route targets.
  - Navigation uses plain anchors in key areas instead of route-aware components.
  - No dedicated routed pages for “ByDirector / ByCountry / ByCompany / ByPerson / ByOrigin / ByLanguage / ByYear”.
  - Legacy grid class intent (`span*`) is mixed into Bootstrap 5 markup and needs normalization.

## Thought-by-Thought Execution Plan

### 1) Lock parity baseline before edits
- Capture a **legacy vs modern page parity matrix** for:
  - Home + search panel
  - Search results list rows
  - Filtered search routes (director/country/company/person/origin/language/year)
  - Film detail page
  - About/FAQ/Contribute/Contact
- Define “match” rules: required fields, link behavior, sort defaults, and visible structure.
- Output: a short checklist that every migrated page must pass.

### 2) Rebuild route map to mirror legacy journeys
- Add modern routes that map old intent to new route patterns (while preserving shareable URLs).
- Support direct entry and refresh for each search path.
- Decide canonical route naming and add redirects/aliases where needed to reduce broken incoming links.
- Output: route contract document + implemented page route stubs.

### 3) Introduce a real frontend search domain layer
- Replace ad hoc per-component querying with a reusable search service that handles:
  - Title search
  - Facet filtering (director/company/country/person/origin/language/year)
  - Sorting options
  - Pagination/infinite load strategy
- Keep this layer deterministic and testable for CSV-in-memory datasets.
- Output: production search service and query model used by all search pages.

### 4) Reach legacy parity for primary search box behavior
- Home search should support:
  - Enter key search
  - Quick submit button
  - Route update on search action
  - Result state restoration when navigating back
- Recreate legacy result-row composition:
  - Film title/year
  - Director links
  - Country links
  - Company links
- Output: home search UX that feels like legacy but routes like modern SPA.

### 5) Implement dedicated “search by X” pages
- Build pages/components for each filter dimension used in legacy routes.
- Ensure each page:
  - Reads route/query params
  - Hydrates selected filter labels
  - Displays the same result card/list format as home search
- Output: complete set of navigable, deep-linkable search views.

### 6) Deliver advanced search (legacy-equivalent, modern interaction)
- Replace placeholder “Advanced” action with an accessible panel/drawer.
- Include legacy filter set: title, origin, director, person, company, country, language, duration, mode (and any currently used export/sort controls).
- Provide clear state controls: apply, clear all, close, preserved state on back/forward.
- Output: advanced search that reproduces legacy power-user workflows.

### 7) Fix film detail page functional parity and link integrity
- Remove hard-coded IDs and generate links from actual current entities.
- Ensure each relationship link routes to the corresponding modern search page.
- Validate cast/crew grouping and ordering against legacy behavior.
- Output: film page that acts as a navigation hub exactly like legacy.

### 8) Normalize layout to legacy look using modern grid/classes
- Replace leftover legacy grid class usage with Bootstrap 5 equivalents while preserving visual proportions.
- Tune spacing, typography, and panel widths to match screenshots/reference pages.
- Ensure nav bars and banner/credit spacing mirror legacy visual rhythm.
- Output: pixel-close layout parity across desktop/tablet/mobile breakpoints.

### 9) Harden navigation UX and accessibility
- Move navigation links to route-aware components where appropriate.
- Add active-state highlighting and proper page titles per route.
- Keyboard/a11y pass for search controls, advanced panel, and result links.
- Output: launch-safe navigation and accessibility baseline.

### 10) Finish content parity pages and static information quality
- Bring Contribute/Contact/About/FAQ content and hierarchy to final production quality.
- Ensure outbound links, route casing, and labels match legacy conventions where intentional.
- Output: no obvious draft pages, no dead links, no placeholder copy.

### 11) Launch-readiness QA pass (frontend)
- Functional checks:
  - all search routes
  - result consistency by filter
  - film detail relational links
  - back/forward behavior
- Visual checks against legacy references.
- Browser/device sanity matrix and GitHub Pages base-path verification.
- Output: final issue list with only launch-blockers remaining.

### 12) Stabilization and release cut
- Prioritize/fix remaining launch-blocker defects.
- Freeze UI copy and route contracts.
- Add a short “known differences from legacy” note only if unavoidable.
- Output: launch candidate frontend branch ready for production publish.

## Suggested Implementation Order (High Value First)
1. Route contract + search service foundation
2. Primary search + result row parity
3. Search-by-X pages
4. Advanced search
5. Film detail link integrity
6. Layout polish + content completion
7. QA + release cut

## Definition of Done (Frontend Launch)
- A legacy user can complete all common search and browse tasks without confusion.
- Every legacy search path has an equivalent modern route and returns expected results.
- Theme/layout is recognizably the same with modern responsive behavior.
- No hard-coded IDs, no dead navigation paths, no draft UI elements.
- Frontend is stable on GitHub Pages with CSV-backed data and production-ready UX.

---

## Next Phase: Execution Checklist (Issue-Sized)

### Sprint A — Search Foundation + Routing Contract

#### A1. Define canonical frontend search routes
- **Files**:
  - `modern/FrontEnd.Web/App.razor`
  - `modern/FrontEnd.Web/Pages/HomeSearch.razor`
  - `modern/FrontEnd.Web/Pages/FilmView.razor`
  - `modern/FrontEnd.Web/Layout/MainLayout.razor`
- **Tasks**:
  - Establish canonical routes for:
    - title search
    - by director/country/company/person/origin/language/year
    - film details
  - Add aliases for legacy-shaped URLs where needed.
  - Ensure route casing conventions are consistent.
- **Acceptance**:
  - Direct URL entry + refresh works for each route.
  - Browser back/forward restores search state.

#### A2. Implement reusable search query/response models
- **Files**:
  - `modern/FrontEnd.Core/Services/SearchService.cs`
  - `modern/FrontEnd.Core` (new models folder if needed)
- **Tasks**:
  - Create query model for title/facets/sort/paging.
  - Create result DTO shape used across all pages.
- **Acceptance**:
  - All search UIs consume same model contract.
  - No duplicated filter logic in page code-behind.

#### A3. Build production SearchService
- **Files**:
  - `modern/FrontEnd.Core/Services/SearchService.cs`
  - `modern/FrontEnd.Web/Program.cs`
- **Tasks**:
  - Replace placeholder service with in-memory LINQ search against hydrated context.
  - Support title + facets + sort.
  - Register service in DI.
- **Acceptance**:
  - Service returns consistent results for equivalent legacy scenarios.

### Sprint B — Home Search Parity

#### B1. Upgrade HomeSearch UX and behavior parity
- **Files**:
  - `modern/FrontEnd.Web/Pages/HomeSearch.razor`
- **Tasks**:
  - Wire button + Enter key to route-driven search.
  - Keep loading state and empty-state behavior stable.
  - Add advanced toggle entrypoint (panel open/close behavior).
- **Acceptance**:
  - User can perform repeated searches without page reload.
  - Search parameters are visible in URL (query or route).

#### B2. Recreate legacy result-row content composition
- **Files**:
  - `modern/FrontEnd.Web/Pages/HomeSearch.razor`
  - optional shared component (`modern/FrontEnd.Web/Components/*`)
- **Tasks**:
  - Render: title/year, director links, country links, company links.
  - Match punctuation/ordering close to legacy list style.
- **Acceptance**:
  - Result list is visually and semantically equivalent to legacy `_FilmList.cshtml` behavior.

### Sprint C — Search-By Pages + Film Detail Integrity

#### C1. Add/complete Search-by-X routed pages
- **Files**:
  - `modern/FrontEnd.Web/Pages/*` (new pages as needed)
- **Tasks**:
  - Implement routes for director/country/company/person/origin/language/year.
  - Resolve incoming ID/GUID param to labels and query filters.
- **Acceptance**:
  - All relationship links across the app land on working result pages.

#### C2. Fix FilmView link correctness and grouping
- **Files**:
  - `modern/FrontEnd.Web/Pages/FilmView.razor`
- **Tasks**:
  - Remove hard-coded IDs.
  - Generate links from actual related entities.
  - Preserve director exclusion from cast grouping.
- **Acceptance**:
  - Every metadata link in film details routes to correct filtered search.

### Sprint D — Advanced Search + Layout Polish

#### D1. Implement advanced search panel
- **Files**:
  - `modern/FrontEnd.Web/Pages/HomeSearch.razor`
  - optional new component(s) under `modern/FrontEnd.Web/Components/*`
- **Tasks**:
  - Add filters: title/origin/director/person/company/country/language/duration/mode.
  - Add apply/clear controls and URL state sync.
- **Acceptance**:
  - Advanced search reproduces legacy filter combinations with modern SPA interactions.

#### D2. Normalize layout classes and nav behavior
- **Files**:
  - `modern/FrontEnd.Web/Layout/MainLayout.razor`
  - `modern/FrontEnd.Web/Layout/NavMenu.razor` (remove unused template behavior)
  - `modern/FrontEnd.Web/wwwroot/css/app.css`
  - `modern/FrontEnd.Web/Pages/Home.razor`
- **Tasks**:
  - Replace leftover legacy grid class usage (`span*`) with Bootstrap 5 grid.
  - Ensure nav active states and spacing match legacy rhythm.
- **Acceptance**:
  - No functional layout dependence on legacy Bootstrap 2 class names.

### Sprint E — Content Finalization + Launch QA

#### E1. Complete content parity pages
- **Files**:
  - `modern/FrontEnd.Web/Pages/Contribute.razor`
  - `modern/FrontEnd.Web/Pages/About.razor`
  - `modern/FrontEnd.Web/Pages/Faq.razor`
  - (add Contact page if required)
- **Tasks**:
  - Remove draft markers and placeholder language.
  - Validate outbound links and page titles.
- **Acceptance**:
  - No draft content visible on production routes.

#### E2. Frontend launch QA checklist run
- **Files**:
  - `modern/update.md` (QA results section)
- **Tasks**:
  - Execute route matrix, visual parity checks, and GitHub Pages base-path checks.
  - Capture only remaining launch blockers.
- **Acceptance**:
  - Issue list is triaged; branch is releasable.

---

## Tracking Board Template (copy into issues)

### Issue Title Pattern
`[Frontend Polish] <Area> - <Short Outcome>`

### Issue Body Template
- **Objective**: 1-sentence user-facing outcome.
- **Files**: explicit paths.
- **Legacy parity target**: what old behavior is being matched.
- **Implementation notes**: approach constraints.
- **Acceptance criteria**: testable checks.
- **Out of scope**: avoid scope creep.

---

## Immediate Next 3 Tickets (Recommended)
1. `[Frontend Polish] SearchService foundation and DI wiring`
2. `[Frontend Polish] HomeSearch route-driven query + legacy result row composition`
3. `[Frontend Polish] FilmView relationship links (remove hard-coded IDs)`
