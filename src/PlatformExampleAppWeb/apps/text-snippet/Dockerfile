# BUILDER START
FROM node:14-alpine as builder
WORKDIR /usr/src

# npm install
COPY package.json .
RUN npm install

# Copy common files
COPY tsconfig.base.json .
COPY nx.json .
COPY angular.json .
COPY .eslintrc.json .
COPY decorate-angular-cli.js .
COPY libs libs
COPY tools tools

# Build app files
COPY apps/text-snippet apps/text-snippet
COPY libs libs
COPY typings typings
ARG configuration
RUN npm run nx build text-snippet -- --configuration=${configuration}
# BUILDER END

# Build static web app
FROM nginx:1.15.1-alpine as app
EXPOSE 80
RUN apk --update add bash && \
    apk add dos2unix

RUN rm -rf /usr/share/nginx/html/*
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=builder /usr/src/dist/apps/text-snippet /usr/share/nginx/html
COPY apps/text-snippet/docker-entrypoint.sh /

RUN chmod 777 -R /docker-entrypoint.sh

#Fix error: exec /docker-entrypoint.sh: no such file or directory related to
#window difference in end-of-line (EOL) characters on Windows and Linux. Windows uses CRLF represent the end of a line and Unix/Linux uses LF.
#References: https://stackoverflow.com/questions/53165471/building-docker-images-on-windows-entrypoint-script-no-such-file-or-directory
RUN dos2unix /docker-entrypoint.sh
ENTRYPOINT [ "/docker-entrypoint.sh" ]
