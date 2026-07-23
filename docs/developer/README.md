# Developer Guides

How to *do things* in PlannerPro, as opposed to why it's shaped the way it is (the [ADRs](../adr/README.md)) or how a mechanism works (the [Pattern Deep Dives](./patterns/README.md)).

> `src/` does not exist yet. These guides describe the procedures against the **designed** architecture that `docs/prompts/scrub-prompts.md` builds. Once the system exists, they should be revised to cite real files. Where a guide describes something that has not been verified in running code, it says so.

| Guide | Read this when… |
| --- | --- |
| [Adding an Endpoint by Hand](./adding-an-endpoint-manually.md) | You want the full controller → repository slice without Claude Code, or you want to understand what the `add-endpoint` skill is automating. |
| [Adding Seed Data](./adding-seed-data.md) | You need development data across several tenants, and need to know why seeding is the one place filters are legitimately bypassed. |
| [Tracing a Slice: Tenant Provisioning](./tracing-a-slice-tenant-provisioning.md) | You're new and want to see one request cross every layer, every service, and the bus. **Read this first.** |
| [Tracing the Outbox: `ProjectCreated`](./tracing-the-outbox-project-created.md) | You want one event followed from the business layer that builds it to the consumers that react. |
| [Pattern Deep Dives](./patterns/README.md) | You're changing a mechanism rather than using one. |
