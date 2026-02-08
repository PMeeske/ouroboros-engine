# Ouroboros Documentation

This directory contains comprehensive technical documentation for Ouroboros.

## Quick Navigation

### 🚀 Getting Started
- [Main README](../README.md) - Project overview, features, and quick start
- [Architecture Documentation](ARCHITECTURE.md) - Detailed system architecture
- [Contributing Guide](CONTRIBUTING.md) - Guidelines for contributing to the project

### 📦 Infrastructure & Configuration

_Note: This repository focuses on the engine layer. For deployment guides and infrastructure documentation, see the main [Ouroboros-v2](https://github.com/PMeeske/Ouroboros-v2) repository._

### 🧠 Architecture & Features

#### Core Architecture
- [**Architectural Layer Diagram**](ARCHITECTURAL_LAYERS.md) - Comprehensive visual system architecture with component diagrams, data flow, and deployment topology
- [**Architecture Documentation**](ARCHITECTURE.md) - Detailed system architecture
- [**Self-Improving Agent**](SELF_IMPROVING_AGENT.md) - Self-improving agent architecture and capabilities
- [**Iterative Refinement Architecture**](ITERATIVE_REFINEMENT_ARCHITECTURE.md) - Refinement loop architecture
- [**Recursive Chunking**](RECURSIVE_CHUNKING.md) - Large context processing with adaptive chunking

#### Specifications & Requirements
- [**Non-Functional Requirements (NFRs)**](specs/v1.0-nfr.md) - v1.0 NFRs with 9 categories (Performance, Security, Reliability, Scalability, Compatibility, Maintainability, Observability, Resource Efficiency, Compliance)

#### Phase Implementation
- [**Phase 2 Implementation Summary**](PHASE2_IMPLEMENTATION_SUMMARY.md) - Metacognition features
- [**Phase 3 Implementation Summary**](PHASE3_IMPLEMENTATION_SUMMARY.md) - Emergent intelligence features

### 🧪 Testing & Quality

- [CI Pipeline](../.github/workflows/ci.yml) - GitHub Actions CI workflow
- [Mutation Testing](../.github/workflows/mutation.yml) - Stryker.NET mutation testing

### 📊 Status & Baselines

- [**Current State Baseline**](status/baseline.md) - Comprehensive baseline inventory for production-ready initiative (Epic #120)

### 👥 Contributing & Development

- [**Contributing Guide**](CONTRIBUTING.md) - Guidelines for contributing to the project
- [**GitHub Copilot Development Loop**](COPILOT_DEVELOPMENT_LOOP.md) - Automated development workflows with AI assistance
- [**Automated Development Cycle**](AUTOMATED_DEVELOPMENT_CYCLE.md) - Fully automated code improvement workflow
- [**Playwright Copilot Assignment**](PLAYWRIGHT_COPILOT_ASSIGNMENT.md) - UI-based automation for GitHub issue assignment 🎭
- [**Architecture Documentation**](ARCHITECTURE.md) - Detailed system architecture
- [**Epic #120 Integration Guide**](Epic120Integration.md) - Epic branch orchestration and v1.0 release coordination
- [**Feature Engineering**](FEATURE_ENGINEERING.md) - Feature extraction and engineering approaches

### 📚 Historical Documentation

The [archive/](archive/) directory contains historical implementation summaries, incident reports, and completed work documentation. These serve as reference material for understanding past decisions but may contain outdated information.

## Documentation Organization

### Root Directory (`/`)
Contains the main project README:
- `README.md` - Main project documentation with overview, build instructions, and navigation
- `LICENSE` - MIT License

### Docs Directory (`/docs`)
Contains detailed technical documentation:
- **Architecture**: Agent features, processing strategies, cognitive patterns
- **Guides**: Development workflows, feature engineering
- **Implementation**: Phase summaries and feature documentation
- **Status**: Baseline assessments and progress tracking
- **API**: API reference documentation

### Archive Directory (`/docs/archive`)
Contains historical documentation:
- Implementation summaries (phases, features)
- Incident reports and resolutions
- Sprint summaries
- Deprecated guides

## Contributing to Documentation

When adding new documentation:

1. **Technical documentation** → `/docs` directory
2. **Historical records** → `/docs/archive` directory
3. **Update this index** when adding new docs
4. **Cross-reference** related documents
5. **Follow markdown best practices**

## Documentation Standards

- Use clear, descriptive titles
- Include a table of contents for long documents
- Provide code examples where applicable
- Keep examples up-to-date with current codebase
- Use consistent formatting and style
- Link to related documentation

---

**Need help?** Start with the [Main README](../README.md) or check the [Architecture Documentation](ARCHITECTURE.md).
