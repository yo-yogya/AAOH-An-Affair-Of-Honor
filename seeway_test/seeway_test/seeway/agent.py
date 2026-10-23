import logging

from dotenv import load_dotenv
from livekit import agents
from livekit.agents import Agent, AgentServer, AgentSession, JobContext, room_io
from livekit.plugins import noise_cancellation, silero
from livekit.agents import (
    Agent,
    AgentServer,
    AgentSession,
    JobContext,
    TurnHandlingOptions,
    inference,
    room_io,
)
from livekit.agents import llm, stt, tts, inference
load_dotenv()


# Define your agent's behavior by extending the Agent class
class Assistant(Agent):
    def __init__(self) -> None:
        super().__init__(
            instructions="you are a helpful voice AI assistant say  at the end of every sentence.",  # System prompt for the LLM
        )


server = AgentServer()


# The entrypoint function runs when a participant joins the room
@server.rtc_session()
async def entrypoint(ctx: JobContext):
    # Configure the voice pipeline with STT, LLM, TTS, and VAD providers
    session = AgentSession(
    # LLM with fallback: OpenAI primary, Gemini backup
    llm=llm.FallbackAdapter(
        [
            inference.LLM(model="openai/gpt-4.1-mini"),
            inference.LLM(model="google/gemini-2.5-flash"),
        ]
    ),
    # STT with fallback: AssemblyAI primary, Deepgram backup
    stt=stt.FallbackAdapter(
        [
            inference.STT.from_model_string("assemblyai/universal-streaming:en"),
            inference.STT.from_model_string("deepgram/nova-3"),
        ]
    ),
    # TTS with fallback: Cartesia primary, Inworld backup
    tts=tts.FallbackAdapter(
        [
            inference.TTS.from_model_string("cartesia/sonic-3:9626c31c-bec5-4cca-baa8-f8ba9e84c8bc"),
            inference.TTS.from_model_string("inworld/inworld-tts-1"),
        ]
    ),
    vad=silero.VAD.load(),                  # Voice activity detection
    turn_handling=TurnHandlingOptions(
        turn_detection=inference.TurnDetector(), 
    ), 
    preemptive_generation=True,

    )

    # Start the session with noise cancellation enabled
    await session.start(
        agent=Assistant(),
        room=ctx.room,
        room_options=room_io.RoomOptions(
            audio_input=room_io.AudioInputOptions(
                noise_cancellation=noise_cancellation.BVC(),  # Background voice cancellation
            ),
        ),
    )


if __name__ == "__main__":
    logging.basicConfig(level=logging.INFO)
    agents.cli.run_app(server)
