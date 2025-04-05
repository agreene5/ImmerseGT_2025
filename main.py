# BACKEND FILE - main.py

from fastapi import FastAPI, File, UploadFile
from ultralytics import YOLO
from fastapi.responses import JSONResponse
import cv2
import numpy as np

# Load YOLOv8s model
model = YOLO("yolov8s.pt")  # make sure this is downloaded

# Optional: Configure detection params
model.overrides['conf'] = 0.25       # Minimum confidence threshold
model.overrides['iou'] = 0.45        # IoU threshold for NMS
model.overrides['agnostic_nms'] = False
model.overrides['max_det'] = 1000    # Max number of detections per image

# Create FastAPI app
app = FastAPI()

@app.post("/upload")
async def upload_image(file: UploadFile = File(...)):
    contents = await file.read()

    # Convert to OpenCV image
    nparr = np.frombuffer(contents, np.uint8)
    img = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

    # Perform detection
    results = model.predict(source=img)
    boxes = results[0].boxes

    output = []
    for box in boxes:
        cls_id = int(box.cls[0])
        label = model.names[cls_id]  # Changed from model.model.names
        conf = float(box.conf[0])
        coords = box.xyxy[0].tolist()

        output.append({
            "label": label,
            "confidence": conf,
            "bbox": coords  # [x1, y1, x2, y2]
        })

    return JSONResponse(content={"detections": output})
