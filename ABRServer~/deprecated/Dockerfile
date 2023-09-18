FROM python:3.8-slim

LABEL maintainer="gda@tacc.utexas.edu"
LABEL version="0.1"
LABEL description="SculptingVis app"

ARG DEBIAN_FRONTEND=noninteractive

# RUN apt update
# RUN echo 'y' | apt install python3.8 python3-pip less
# RUN python3.8 -m pip install --upgrade pip

COPY ./requirements.txt requirements.txt
RUN python3.8 -m pip install -r requirements.txt

RUN mkdir abr_server
COPY . abr_server

RUN cd abr_server ; python3.8 manage.py collectstatic --noinput

CMD cd abr_server ; python3.8 manage.py runserver --noreload 0.0.0.0:8000
EXPOSE 8000