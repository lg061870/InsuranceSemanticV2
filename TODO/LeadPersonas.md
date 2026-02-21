# Lead Personas & User Stories

This document lists expected visitor types and user stories for the insurance lead capture site. Keep this as a living reference for product, UX, and engineering decisions.

## Context
This site is set up to capture insurance leads. We expect many different types of users — anyone who shows interest in insurance should be able to provide some information. In the best case we should connect a user directly to a live agent.

---

## 1. First-Time / Curious Visitor (Cold traffic, ads, SEO, referrals)

- US-01 — Understand the value quickly
  - As a visitor, I want to immediately understand what the site offers and how it helps me, so I can decide whether to keep reading.

- US-02 — See credibility and trust signals
  - As a visitor, I want to see licenses, reviews, carriers, or credentials, so I can trust the site before sharing personal information.

- US-03 — Know who the insurance is for
  - As a visitor, I want to know whether the insurance applies to someone like me (age, family, mortgage, business, etc.), so I don’t waste time.

- US-04 — Learn without committing
  - As a visitor, I want to browse basic explanations of insurance types without giving my contact info, so I feel comfortable continuing.

---

## 2. Education-Driven Visitor (Research mode, comparison mode)

- US-05 — Learn insurance basics
  - As a visitor, I want simple explanations of different insurance types, so I can understand my options.

- US-06 — Compare insurance types
  - As a visitor, I want to compare term, whole, and universal life insurance, so I can decide what fits my goals.

- US-07 — Understand “why I need this”
  - As a visitor, I want examples of real-life situations (mortgage, kids, income protection), so I can see how insurance applies to me.

- US-08 — Avoid sales pressure
  - As a visitor, I want to explore at my own pace without aggressive sales messaging, so I don’t feel pushed.

---

## 3. Goal-Oriented Visitor (Knows they need insurance, just not which one)

- US-09 — Get guidance based on my goals
  - As a visitor, I want the site to ask about my life goals, so it can guide me to the right insurance type.

- US-10 — Get a recommendation, not a quote yet
  - As a visitor, I want a recommendation before seeing prices, so I understand what I should be looking for.

- US-11 — See scenarios tailored to me
  - As a visitor, I want to see examples that match my situation (family, debts, income), so the advice feels relevant.

---

## 4. Quote-Ready Visitor (High intent, conversion-ready)

- US-12 — Get a quick estimate
  - As a visitor, I want a fast, simple estimate without filling a long form, so I can gauge affordability.

- US-13 — Control how much I share
  - As a visitor, I want to share minimal personal information at first, so I feel safe.

- US-14 — Understand next steps clearly
  - As a visitor, I want to know exactly what happens after I submit my info, so I don’t feel trapped.

- US-15 — Choose how to be contacted
  - As a visitor, I want to choose email, phone, or chat follow-up, so I stay in control.

---

## 5. AI / Assistant-Driven Visitor (Chat-first experience)

- US-16 — Ask questions conversationally
  - As a visitor, I want to ask insurance questions in plain language, so I don’t need industry knowledge.

- US-17 — Get personalized answers
  - As a visitor, I want the assistant to remember what I already told it, so I don’t repeat myself.

- US-18 — Be guided step-by-step
  - As a visitor, I want the assistant to guide me gradually, so the process doesn’t feel overwhelming.

- US-19 — Stop and resume later
  - As a visitor, I want to resume my conversation later, so I don’t lose progress.

---

## 6. Privacy-Concerned Visitor (Skeptical but interested)

- US-20 — Know how my data is used
  - As a visitor, I want clear explanations of how my data is handled, so I feel safe submitting information.

- US-21 — Avoid spam
  - As a visitor, I want reassurance I won’t be spammed, so I’m willing to continue.

---

## 7. Returning Visitor / Lead (Warm traffic)

- US-22 — Pick up where I left off
  - As a returning visitor, I want to continue from my last step, so I don’t start over.

- US-23 — Refine my options
  - As a returning visitor, I want to adjust coverage amounts or preferences, so results better match my needs.

- US-24 — Speak to a human if needed
  - As a returning visitor, I want the option to talk to a licensed agent, so I can confirm my decision.

---

## 8. Conversion & Trust Closure (Final push)

- US-25 — Feel confident before submitting
  - As a visitor, I want reassurance that I’m making a good decision, so I feel comfortable converting.

- US-26 — See clear benefits of acting now
  - As a visitor, I want to understand why acting now matters, so I don’t postpone indefinitely.

---

## Notes & Future Development
- Treat framework assets as defaults only; domain apps must be able to override assets and copy domain-specific branding.
- Consider adding a small `AvatarUrl` parameter on the chat component and/or an `IAvatarProvider` interface to enable domain developers to supply images safely.
- Next steps: review this doc with product/domain team and convert prioritized US items into backlog tickets with acceptance criteria.
