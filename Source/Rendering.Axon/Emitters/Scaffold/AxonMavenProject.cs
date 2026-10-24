// Copyright (c) Cratis. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Cratis.Stage.Rendering.Axon.Emitters.Scaffold;

internal static class AxonMavenProject
{
    internal static string Pom(string package) => $$"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!-- Copyright (c) Cratis. All rights reserved. Licensed under the MIT license. -->
        <project xmlns="http://maven.apache.org/POM/4.0.0" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 https://maven.apache.org/xsd/maven-4.0.0.xsd">
          <modelVersion>4.0.0</modelVersion>
          <parent><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-parent</artifactId><version>4.1.2</version><relativePath/></parent>
          <groupId>{{package}}</groupId><artifactId>{{package}}-application</artifactId><version>1.0.0-SNAPSHOT</version>
          <properties><java.version>21</java.version><project.build.sourceEncoding>UTF-8</project.build.sourceEncoding></properties>
          <dependencyManagement><dependencies>
            <dependency><groupId>org.axonframework</groupId><artifactId>axon-framework-bom</artifactId><version>5.4.1</version><type>pom</type><scope>import</scope></dependency>
          </dependencies></dependencyManagement>
          <dependencies>
            <dependency><groupId>org.axonframework.extensions.spring</groupId><artifactId>axon-spring-boot-starter</artifactId></dependency>
            <dependency><groupId>io.projectreactor</groupId><artifactId>reactor-core</artifactId></dependency>
            <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-webmvc</artifactId></dependency>
            <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-security</artifactId></dependency>
            <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-validation</artifactId></dependency>
            <dependency><groupId>org.springframework.boot</groupId><artifactId>spring-boot-starter-webmvc-test</artifactId><scope>test</scope></dependency>
            <dependency><groupId>org.springframework.security</groupId><artifactId>spring-security-test</artifactId><scope>test</scope></dependency>
            <dependency><groupId>org.axonframework</groupId><artifactId>axon-test</artifactId><scope>test</scope></dependency>
            <dependency><groupId>org.axonframework.extensions.spring</groupId><artifactId>axon-spring-boot-starter-test</artifactId><scope>test</scope></dependency>
          </dependencies>
          <build><plugins>
            <plugin><groupId>org.springframework.boot</groupId><artifactId>spring-boot-maven-plugin</artifactId></plugin>
            <plugin><groupId>org.apache.maven.plugins</groupId><artifactId>maven-compiler-plugin</artifactId><configuration><parameters>true</parameters><compilerArgs><arg>-Xlint:all</arg><arg>-Werror</arg></compilerArgs></configuration></plugin>
          </plugins></build>
        </project>
        """ + "\n";
}
