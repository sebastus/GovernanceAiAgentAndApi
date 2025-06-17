import azure.functions as func
import logging
from azure.ai.projects import AIProjectClient
from azure.identity import DefaultAzureCredential
import os
import json

app = func.FunctionApp(http_auth_level=func.AuthLevel.ANONYMOUS)

@app.route(route="agent_httptrigger")
def agent_httptrigger(req: func.HttpRequest) -> func.HttpResponse:
    logging.info('Python HTTP trigger function processed a request.')

    message = req.params.get('message')
    agentid = req.params.get('agentid')
    threadid = req.params.get('threadid')
    
    if not message or not agentid:
        try:
            req_body = req.get_json()
        except ValueError:
            req_body = None

        if req_body:
            message = req_body.get('message')
            agentid = req_body.get('agentid')
            threadid = req_body.get('threadid')

    if not message or not agentid:
        return func.HttpResponse(
            "Pass in a message and agentid in the query string or in the request body for a personalized response.",
            status_code=400
        )

    conn_str = os.environ.get("AIProjectConnString")
    if not conn_str:
        logging.error("AIProjectConnString is not set in local.settings.json or environment variables.")
        return func.HttpResponse(
            "Internal Server Error: Missing AIProjectConnString.",
            status_code=500
        )

    try:            
        project_client = AIProjectClient(
            credential=DefaultAzureCredential(),
            endpoint=conn_str,
        )

        agent = project_client.agents.get_agent(agentid)
        if not agent:
            logging.error(f"Agent with ID {agentid} not found.")
            return func.HttpResponse(
                f"Agent with ID {agentid} not found.",
                status_code=404
            )

        # Fix for the 'create_thread' method issue
        if not threadid:
            # Create a new thread using the correct API
            try:
                # Try the newer API if available
                thread_response = project_client.agents.create_thread()
                thread_id = thread_response.id
            except AttributeError:
                # Fallback to direct REST API call if needed
                logging.info("Using alternative method to create thread")
                thread_response = project_client.agents.threads.create()
                thread_id = thread_response.id
        else:
            thread_id = threadid
            
        # Create a message in the thread
        message = project_client.agents.messages.create(
            thread_id=thread_id,
            role="user",
            content=message
        )

        # Process the message with the agent
        project_client.agents.runs.create_and_process(
            thread_id=thread_id,
            agent_id=agent.id
        )

        # Get the messages from the thread
        messages = project_client.agents.messages.list(thread_id=thread_id)
        assistant_messages = [m for m in messages if m["role"] == "assistant"]
        if assistant_messages:
            assistant_message = assistant_messages[-1]
            assistant_text = " ".join(
                part["text"]["value"] for part in assistant_message["content"] if "text" in part
            )
        else:
            assistant_text = "No assistant message found."

        # Return the response with the thread ID for continuity
        #response_data = {
        #    "message": assistant_text,
        #    "threadId": thread_id
        #}
        
        return func.HttpResponse(
            json.dumps(assistant_text),
            status_code=200,
            mimetype="application/json"
        )
    except Exception as e:
        logging.error(f"An error occurred: {str(e)}")
        # Include more detailed error information for debugging
        import traceback
        logging.error(traceback.format_exc())
        return func.HttpResponse(
            "Internal Server Error: " + str(e),
            status_code=500
        )