import java.io.BufferedWriter;
import java.io.IOException;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Map;

public final class VanillaBiomeDump {
    private VanillaBiomeDump() {
    }

    public static void main(String[] args) throws Exception {
        if (args.length != 1) {
            throw new IllegalArgumentException("Expected output path argument.");
        }

        Object overworldValues = loadOverworldValues();
        Path outputPath = Path.of(args[0]);
        Files.createDirectories(outputPath.getParent());
        writeEntries(outputPath, (List<?>)overworldValues);
    }

    private static Object loadOverworldValues() throws Exception {
        Class<?> parameterListClass = Class.forName("net.minecraft.world.level.biome.MultiNoiseBiomeSourceParameterList");
        Method knownPresetsMethod = parameterListClass.getMethod("m_274368_");
        Map<?, ?> knownPresets = (Map<?, ?>)knownPresetsMethod.invoke(null);

        Object overworldParameterList = null;
        for (Map.Entry<?, ?> entry : knownPresets.entrySet()) {
            if (entry.getKey().toString().contains("overworld")) {
                overworldParameterList = entry.getValue();
                break;
            }
        }

        if (overworldParameterList == null) {
            throw new IllegalStateException("Could not find overworld parameter list.");
        }

        Field valuesField = findFieldByType(overworldParameterList.getClass(), List.class);
        valuesField.setAccessible(true);
        return valuesField.get(overworldParameterList);
    }

    private static void writeEntries(Path outputPath, List<?> values) throws Exception {
        Class<?> climateClass = Class.forName("net.minecraft.world.level.biome.Climate");
        Method unquantizeMethod = climateClass.getMethod("m_186796_", long.class);

        try (BufferedWriter writer = Files.newBufferedWriter(outputPath, StandardCharsets.UTF_8)) {
            writer.write("{\"entries\":[");
            for (int i = 0; i < values.size(); i++) {
                if (i > 0) {
                    writer.write(',');
                }

                Object pair = values.get(i);
                Object point = invokeNoArg(pair, "getFirst");
                Object biome = invokeNoArg(pair, "getSecond");
                String biomeName = resolveBiomeName(biome);

                List<Field> parameterFields = findFieldsByType(point.getClass(), "net.minecraft.world.level.biome.Climate$Parameter");
                if (parameterFields.size() != 6) {
                    throw new IllegalStateException("Expected 6 climate parameter fields, got " + parameterFields.size());
                }

                Field offsetField = findFirstLongField(point.getClass(), parameterFields);
                offsetField.setAccessible(true);

                writer.write("{\"biome\":\"");
                writer.write(biomeName);
                writer.write("\",");
                writeParameter(writer, "temperature", parameterFields.get(0).get(point), unquantizeMethod);
                writer.write(',');
                writeParameter(writer, "humidity", parameterFields.get(1).get(point), unquantizeMethod);
                writer.write(',');
                writeParameter(writer, "continentalness", parameterFields.get(2).get(point), unquantizeMethod);
                writer.write(',');
                writeParameter(writer, "erosion", parameterFields.get(3).get(point), unquantizeMethod);
                writer.write(',');
                writeParameter(writer, "depth", parameterFields.get(4).get(point), unquantizeMethod);
                writer.write(',');
                writeParameter(writer, "weirdness", parameterFields.get(5).get(point), unquantizeMethod);
                writer.write(",\"offset\":");
                writer.write(Float.toString(((Float)unquantizeMethod.invoke(null, offsetField.getLong(point))).floatValue()));
                writer.write('}');
            }
            writer.write("]}");
        }
    }

    private static void writeParameter(BufferedWriter writer, String name, Object parameter, Method unquantizeMethod) throws Exception {
        Field[] fields = parameter.getClass().getDeclaredFields();
        List<Field> longFields = new ArrayList<>();
        for (Field field : fields) {
            if (field.getType() == long.class) {
                field.setAccessible(true);
                longFields.add(field);
            }
        }

        if (longFields.size() != 2) {
            throw new IllegalStateException("Expected 2 long fields for parameter, got " + longFields.size());
        }

        long min = longFields.get(0).getLong(parameter);
        long max = longFields.get(1).getLong(parameter);

        writer.write('\"');
        writer.write(name);
        writer.write("\":{\"min\":");
        writer.write(Float.toString(((Float)unquantizeMethod.invoke(null, min)).floatValue()));
        writer.write(",\"max\":");
        writer.write(Float.toString(((Float)unquantizeMethod.invoke(null, max)).floatValue()));
        writer.write('}');
    }

    private static String resolveBiomeName(Object biome) throws Exception {
        Method locationMethod = null;
        for (Method method : biome.getClass().getMethods()) {
            if (method.getParameterCount() == 0 && method.getReturnType().getName().equals("net.minecraft.resources.Identifier")) {
                locationMethod = method;
                break;
            }
        }

        if (locationMethod == null) {
            return biome.toString();
        }

        Object identifier = locationMethod.invoke(biome);
        return identifier.toString();
    }

    private static Field findFieldByType(Class<?> type, Class<?> fieldType) {
        for (Field field : type.getDeclaredFields()) {
            if (fieldType.isAssignableFrom(field.getType())) {
                return field;
            }
        }

        throw new IllegalStateException("Could not find field of type " + fieldType.getName() + " on " + type.getName());
    }

    private static List<Field> findFieldsByType(Class<?> type, String fieldTypeName) {
        List<Field> result = new ArrayList<>();
        for (Field field : type.getDeclaredFields()) {
            if (field.getType().getName().equals(fieldTypeName)) {
                field.setAccessible(true);
                result.add(field);
            }
        }

        return result;
    }

    private static Field findFirstLongField(Class<?> type, List<Field> excludedFields) {
        for (Field field : type.getDeclaredFields()) {
            if (field.getType() == long.class && !excludedFields.contains(field)) {
                return field;
            }
        }

        throw new IllegalStateException("Could not find offset field on " + type.getName());
    }

    private static Object invokeNoArg(Object instance, String methodName) throws Exception {
        Method method = instance.getClass().getMethod(methodName);
        return method.invoke(instance);
    }
}
