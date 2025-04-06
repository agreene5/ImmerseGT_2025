from fastapi import FastAPI, File, UploadFile
from fastapi.responses import JSONResponse, FileResponse
from ultralytics import YOLO
from gtts import gTTS
import cv2
import numpy as np
import os
import uuid
from fastapi import Form


app = FastAPI()


# Load YOLOv8s model
model = YOLO("yolov8s.pt")
model.overrides['conf'] = 0.25
model.overrides['iou'] = 0.45
model.overrides['agnostic_nms'] = False
model.overrides['max_det'] = 1000


@app.post("/upload")
async def upload_image(file: UploadFile = File(...)):
    contents = await file.read()


    nparr = np.frombuffer(contents, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)


    results = model.predict(source=img)
    boxes = results[0].boxes


    output = []
    for box in boxes:
        cls_id = int(box.cls[0])
        label = model.names[cls_id]
        conf = float(box.conf[0])
        coords = box.xyxy[0].tolist()


        output.append({
            "label": label,
            "confidence": conf,
            "bbox": coords
        })


    return JSONResponse(content={"detections": output})


@app.post("/tts")
async def generate_audio(text: str = Form(...)):
    tts = gTTS(text)
    filename = f"tts_{uuid.uuid4().hex}.mp3"
    path = os.path.join("tts_audio", filename)



    os.makedirs("tts_audio", exist_ok=True)
    tts.save(path)


    return FileResponse(path, media_type="audio/mpeg", filename=filename)



