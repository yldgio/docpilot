i want to create an agent orchestration for creating the documentation related to a repository. i want to use github copilot cli or its SDK.
Use cases: 
1. Update existing documentation based on code changes (ie PR).
2. Update / Create new documentation for new features or modules.
3. Improve existing documentation for clarity, grammar, and completeness.

Workflows:
**Maintenance Documentation Update Workflow:**

1. the agent should be able to monitor pull requests and code changes in the repository.
2. upon detecting changes, the agent should analyze the code diffs to identify relevant documentation sections that need updates.
3. the agent should generate or update documentation using GitHub Copilot's capabilities, ensuring that the content is accurate and comprehensive.
4. the agent should create a new pull request with the updated or new documentation for review by the repository maintainers.

**Initial Documentation Creation Workflow:**

1. when new features or modules are added to the repository, the agent should detect these additions.
2. the agent should analyze the new code to understand its functionality and purpose.
3. the agent should generate initial documentation for the new features or modules, covering key aspects such as usage, parameters, and examples.
4. the agent should create a pull request with the newly generated documentation for review by the repository maintainers.

**Documentation Improvement Workflow:**

1. the agent should periodically review existing documentation in the repository.
2. the agent should analyze the documentation for clarity, grammar, and completeness.
3. the agent should suggest improvements and generate revised documentation using GitHub Copilot's capabilities.
4. the agent should create a pull request with the improved documentation for review by the repository maintainers.

**Documentation Areas:**

- README files
- API documentation
- Technical guides
- User manuals
- Changelogs
- Architecture diagrams and explanations
- Troubleshooting guides & Maintenance guides
- Infrastructure documentation 
- Operations
- DevOps processes
- Onboarding documentation for new contributors

**Implementation:**

- Multi-agent system where each workflow is handled by a dedicated agent.
- Use GitHub Copilot CLI or SDK to leverage AI capabilities for generating and improving documentation.
- Integrate with GitHub Actions or other CI/CD tools to automate the monitoring of code changes and triggering of workflows.
- Ensure proper review and approval processes are in place for all generated documentation before merging into the main branch.