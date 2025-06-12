import os
import jsonref
from dotenv import load_dotenv
from azure.identity import DefaultAzureCredential
from azure.ai.projects import AIProjectClient
from azure.ai.agents.models import (
    OpenApiTool,
    OpenApiAnonymousAuthDetails,
    OpenApiConnectionAuthDetails,
    OpenApiConnectionSecurityScheme
)

# Load environment variables from .env
load_dotenv()

# Constants
MODEL_NAME = "gpt-4o-mini"
AGENT_NAME = "Azure Policy Exemptions Agent"
AGENT_ENV_KEY = "EXEMPT_AGENT_ID"
OPENAPI_SPEC_PATH = os.path.join(os.path.dirname(__file__), "tools/exemptions_tool.json")

INSTRUCTIONS = """When I ask you to give me the list of policy exemptions associated with a subscription id - that I will provide - use the relevant action tool to get that list and send it back to me

The subscription id is a guid. If you are provided with anything else, send a message saying that you need the id, not a description of any other sort.

When I ask you to get the details of a specific policy exemption, use the relevant action tool to get that information and send it back to me. This action also requires a subscription id, which I will provide. It also requires the policy exemption name, which I will provide. The policy exemption name is a string, not a guid. Use the "name" field from the list of policy exemptions that you have retrieved, not the "displayName" or any other field.

I may ask you to get the details of one of the policy exemptions that you have listed. Use the same subscription id that you used to get the list of policy exemptions, and the name of the policy exemption that you can get from the list. For example, I could say "get the details of the first policy exemption in the list" or "get the details of the policy exemption named 'my-exemption'".

If I ask you for exemptions within a certain number of days until expiry, put that number of days into the "withExpiryDateWithinDays" field of the action tool. When I make this request, you should first get the current date and time in UTC, and then calculate the max expiry date by adding the number of days to the current date.

The expiry date is expressed as an ISO 8601 date string, like "2024-12-31T23:59:59Z".

You can get the correct current date and time in UTC by using the relevant action tool. It's name is "GetCurrentTime".

I may ask you to update a policy exemption. In this case, I will provide you with the subscription id, the name of the policy exemption, and new expiry date that I want to update. You should use the "UpdateExpiryDate" action tool to perform this action. I will provide you with the new expiry date in ISO 8601 format, like "2024-12-31T23:59:59Z", or I may ask you to simply add some number of days or months to the current expiry date. You should not change anything else of the policy exemption, only the expiry date. When you call the tool to update the expiry date, you must provide the subscription id, the name of the policy exemption, and the new expiry date in ISO 8601 format. The name of the expiry date parameter is "expiresOnIso8601". The expiry date should be provided to the tool as a query string.

These are the only functions that you perform. If you are asked to do anything else - like write a poem or tell a joke, or whatever, just say it's not in your job description.
```"""


def get_project_endpoint() -> str:
    endpoint = os.getenv("PROJECT_ENDPOINT")
    if not endpoint:
        raise EnvironmentError("PROJECT_ENDPOINT is not set in the environment.")
    return endpoint


def create_project_client(endpoint: str) -> AIProjectClient:
    return AIProjectClient(endpoint=endpoint, credential=DefaultAzureCredential())


def load_openapi_tool() -> OpenApiTool:
    with open(OPENAPI_SPEC_PATH, "r") as f:
        spec = jsonref.loads(f.read())

    # auth = OpenApiAnonymousAuthDetails()
    auth = OpenApiConnectionAuthDetails(security_scheme=OpenApiConnectionSecurityScheme(connection_id=os.environ["CONNECTION_ID"]))

    return OpenApiTool(
        name="exemptions", 
        spec=spec, 
        description="Manage Azure Policy Exemptions", 
        auth=auth)


def create_agent(client: AIProjectClient, openapi_tool: OpenApiTool):
    agent = client.agents.create_agent(
        model=MODEL_NAME,
        name=AGENT_NAME,
        tools=openapi_tool.definitions,
        instructions=INSTRUCTIONS
    )

    with open("../.env", "a") as f:
        f.write(f"\n{AGENT_ENV_KEY}={agent.id}")
    return agent


def update_agent(client: AIProjectClient, agent_id: str, openapi_tool: OpenApiTool):
    return client.agents.update_agent(
        agent_id=agent_id,
        model=MODEL_NAME,
        name=AGENT_NAME,
        tools=openapi_tool.definitions,
        instructions=INSTRUCTIONS
    )


def main():
    endpoint = get_project_endpoint()
    client = create_project_client(endpoint)
    tool = load_openapi_tool()

    agent_id = os.getenv(AGENT_ENV_KEY)
    if not agent_id:
        agent = create_agent(client, tool)
    else:
        agent = update_agent(client, agent_id, tool)

    print(f"{AGENT_NAME} ready with ID: {agent.id}")


if __name__ == "__main__":
    main()