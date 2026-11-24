# The validate step of universal-temporal-value's zizd pipeline (see ../README.md). Python 3.12, as validate.yml uses.
FROM python:3.12.16-slim-bookworm@sha256:44cf1793efa7c7a476bb0f2cff81443f4a072c90c1cb127e2924933daa3e8c64
COPY requirements.txt /requirements.txt
RUN pip install --no-cache-dir --root-user-action=ignore -r /requirements.txt
COPY run.sh /run.sh
CMD ["bash", "/run.sh"]
